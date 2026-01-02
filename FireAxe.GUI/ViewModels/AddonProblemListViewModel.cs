using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

namespace FireAxe.ViewModels;

public sealed class AddonProblemListViewModel : ViewModelBase, IActivatableViewModel, IValidity
{
    public class ProblemViewModel : ViewModelBase, IValidity
    {
        private readonly ObservableValidRefCollection<AddonProblem> _problems = new();

        private readonly ReadOnlyObservableCollection<AddonNodeSimpleViewModel> _addonViewModels;

        internal ProblemViewModel(AddonProblem problem)
        {
            ProblemType = problem.GetType();
            _problems.Add(problem);

            _problems.ToObservableChangeSet<ObservableValidRefCollection<AddonProblem>, AddonProblem>()
                .DistinctValues(problem => problem.Addon)
                .Transform(addon => new AddonNodeSimpleViewModel(addon))
                .Bind(out _addonViewModels)
                .Subscribe();

            _problems.WhenAnyValue(x => x.Count)
                .Subscribe(count =>
                {
                    if (count == 0)
                    {
                        IsValid = false;
                    }
                });
        }

        public bool IsValid { get; private set => this.RaiseAndSetIfChanged(ref field, value); } = true;

        public Type ProblemType { get; }

        public string Description => AddonProblemTypeExplanations.Get(ProblemType);

        public ReadOnlyObservableCollection<AddonNodeSimpleViewModel> AddonViewModels => _addonViewModels;

        internal void AddProblem(AddonProblem problem)
        {
            if (problem.GetType() != ProblemType)
            {
                return;
            }
            if (_problems.Contains(problem))
            {
                return;
            }
            _problems.Add(problem);
        }

        internal void DisposeSubscriptions()
        {
            _problems.Dispose();
            IsValid = false;
        }
    }

    public class ProblematicAddonViewModel : AddonNodeSimpleViewModel, IValidity
    {
        private readonly IDisposable _addonSubscription;

        public ProblematicAddonViewModel(AddonNode addon) : base(addon)
        {
            _addonSubscription = addon.Problems.WhenAnyValue(x => x.Count)
                .Subscribe(count =>
                {
                    if (count == 0)
                    {
                        IsValid = false;
                    }
                });

            this.WhenAnyValue(x => x.Addon)
                .Subscribe(addon =>
                {
                    if (addon is null)
                    {
                        IsValid = false;
                    }
                });

            this.RegisterInvalidHandler(() => _addonSubscription.Dispose());
        }

        public bool IsValid { get; private set => this.RaiseAndSetIfChanged(ref field, value); } = true;

        internal void DisposeSubscriptions()
        {
            IsValid = false;
        }
    }

    private ObservableValidRefCollection<ProblemViewModel>? _problemViewModels = null;

    private ObservableValidRefCollection<ProblematicAddonViewModel>? _problematicAddonViewModels = null;

    public AddonProblemListViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        AddonRoot = addonRoot;

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            var addonRoot = AddonRoot;

            addonRoot.RegisterInvalidHandler(() => IsValid = false)
                .DisposeWith(disposables);
            if (!IsValid)
            {
                return;
            }

            _problemViewModels = new();
            ProblemViewModels = _problemViewModels.AsReadOnlyObservableCollection();
            Disposable.Create(() =>
            {
                _problemViewModels.Dispose();
                foreach (var problemViewModel in _problemViewModels)
                {
                    problemViewModel.DisposeSubscriptions();
                }
                _problemViewModels = null;
                ProblemViewModels = ReadOnlyObservableCollection<ProblemViewModel>.Empty;
            }).DisposeWith(disposables);

            _problematicAddonViewModels = new();
            ProblematicAddonViewModels = _problematicAddonViewModels.AsReadOnlyObservableCollection();
            Disposable.Create(() =>
            {
                _problematicAddonViewModels.Dispose();
                foreach (var problematicAddonViewModel in _problematicAddonViewModels)
                {
                    problematicAddonViewModel.DisposeSubscriptions();
                }
                _problematicAddonViewModels = null;
                ProblematicAddonViewModels = ReadOnlyObservableCollection<ProblematicAddonViewModel>.Empty;
            }).DisposeWith(disposables);

            addonRoot.ProblemProduced += NotifyProblemProduced;
            Disposable.Create(() => addonRoot.ProblemProduced -= NotifyProblemProduced);

            foreach (var addon in addonRoot.GetDescendants())
            {
                foreach (var problem in addon.Problems)
                {
                    NotifyProblemProduced(problem);
                }
            }
        });
    }

    public ViewModelActivator Activator { get; } = new();

    public bool IsValid { get; private set => this.RaiseAndSetIfChanged(ref field, value); } = true;

    public AddonRoot AddonRoot { get; }

    public ReadOnlyObservableCollection<ProblemViewModel> ProblemViewModels
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = ReadOnlyObservableCollection<ProblemViewModel>.Empty;

    public ReadOnlyObservableCollection<ProblematicAddonViewModel> ProblematicAddonViewModels
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = ReadOnlyObservableCollection<ProblematicAddonViewModel>.Empty;

    public void Check()
    {
        AddonRoot.Check();
    }

    private void NotifyProblemProduced(AddonProblem problem)
    {
        if (problem is AddonChildrenProblem)
        {
            return;
        }

        if (_problemViewModels is not null)
        {
            var problemType = problem.GetType();
            bool problemTypePresent = false;
            foreach (var problemViewModel in _problemViewModels)
            {
                if (problemViewModel.ProblemType == problemType)
                {
                    problemTypePresent = true;
                    problemViewModel.AddProblem(problem);
                    break;
                }
            }
            if (!problemTypePresent)
            {
                _problemViewModels.Add(new ProblemViewModel(problem));
            }
        }

        if (_problematicAddonViewModels is not null)
        {
            bool addonPresent = false;
            var problematicAddonViewModels = _problematicAddonViewModels.ToArray();
            foreach (var problematicAddonViewModel in problematicAddonViewModels)
            {
                if (problematicAddonViewModel.Addon == problem.Addon)
                {
                    addonPresent = true;
                    break;
                }
            }
            if (!addonPresent)
            {
                _problematicAddonViewModels.Add(new ProblematicAddonViewModel(problem.Addon));
            }
        }
    }
}