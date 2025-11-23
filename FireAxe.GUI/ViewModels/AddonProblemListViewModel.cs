using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;

namespace FireAxe.ViewModels;

public sealed class AddonProblemListViewModel : ViewModelBase, IActivatableViewModel, IDisposable, IValidity
{
    public class ProblemViewModel : ViewModelBase
    {
        private readonly AddonNodeSimpleViewModel[] _addonViewModels;

        internal ProblemViewModel(Type problemType, IEnumerable<AddonNode> addons)
        {
            ProblemType = problemType;
            _addonViewModels = [.. addons.Select(addon => new AddonNodeSimpleViewModel(addon))];
        }

        public Type ProblemType { get; }

        public string Description => AddonProblemTypeExplanations.Get(ProblemType);

        public IReadOnlyCollection<AddonNodeSimpleViewModel> AddonViewModels => _addonViewModels;
    }

    private bool _disposed = false;
    private bool _valid = true;
    private bool _active = false;

    private readonly CancellationTokenSource _cts = new();

    private IEnumerable<AddonNodeSimpleViewModel> _problematicAddonViewModels = [];

    private IEnumerable<ProblemViewModel> _problemViewModels = [];

    public AddonProblemListViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);
        AddonRoot = addonRoot;

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            var addonRoot = AddonRoot;
            
            addonRoot.RegisterInvalidHandler(() => IsValid = false)
                .DisposeWith(disposables);
            if (!IsValid)
            {
                return;
            }

            _active = true;

            Disposable.Create(() =>
            {
                _active = false;
            }).DisposeWith(disposables);

            RefreshCommand.Execute().Subscribe();
        });
    }

    public ViewModelActivator Activator { get; } = new();

    public bool IsValid
    {
        get => _valid;
        private set => this.RaiseAndSetIfChanged(ref _valid, value);
    }

    public AddonRoot AddonRoot { get; }

    public IEnumerable<AddonNodeSimpleViewModel> ProblematicAddonViewModels
    {
        get => _problematicAddonViewModels;
        private set => this.RaiseAndSetIfChanged(ref _problematicAddonViewModels, value);
    }

    public IEnumerable<ProblemViewModel> ProblemViewModels
    {
        get => _problemViewModels;
        private set => this.RaiseAndSetIfChanged(ref _problemViewModels, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public Interaction<Exception, Unit> ShowExceptionInteraction { get; } = new();

    private async Task RefreshAsync()
    {
        this.ThrowIfInvalid();

        ProblematicAddonViewModels = [];
        ProblemViewModels = [];
        var cancellationToken = _cts.Token;
        var addonRoot = AddonRoot;
        
        try
        {
            await addonRoot.CheckAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (_active)
            {
                await ShowExceptionInteraction.Handle(ex);
            }
        }

        var problematicAddonViewModels = new List<AddonNodeSimpleViewModel>();
        var problemToAddons = new Dictionary<Type, List<AddonNode>>();
        foreach (var addon in addonRoot.GetDescendants())
        {
            var problems = addon.Problems;

            if (problems.Count == 0)
            {
                continue;
            }
            if (addon is AddonGroup && problems is [AddonChildrenProblem _])
            {
                continue;
            }

            problematicAddonViewModels.Add(new AddonNodeSimpleViewModel(addon));

            foreach (var problem in problems)
            {
                if (problem is AddonChildrenProblem)
                {
                    continue;
                }

                var problemType = problem.GetType();
                if (!problemToAddons.TryGetValue(problemType, out var correspondingAddons))
                {
                    correspondingAddons = new List<AddonNode>();
                    problemToAddons[problemType] = correspondingAddons;
                }
                correspondingAddons.Add(addon);
            }
        }
        ProblematicAddonViewModels = problematicAddonViewModels.ToArray();
        ProblemViewModels = problemToAddons.Select(pair => new ProblemViewModel(pair.Key, pair.Value)).ToArray();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            IsValid = false;

            _cts.Cancel();
            _cts.Dispose();

            _disposed = true;
        }
    }
}