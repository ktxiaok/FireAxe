using Serilog;
using System;
using System.Text.RegularExpressions;

namespace FireAxe
{
    public static class AddonNodeSearchUtils
    {
        public static Task SearchAsync(IEnumerable<AddonNode> addonNodes, string searchText, AddonNodeSearchOptions options, Action<AddonNode> consumer, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(addonNodes);
            ArgumentNullException.ThrowIfNull(searchText);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(consumer);

            var searchNodes = new List<SearchNode>();
            foreach (var addonNode in addonNodes)
            {
                searchNodes.Add(SearchNode.Build(addonNode, options));
            }
            return Task.Run(() =>
            {
                if (options.IsFlatten)
                {
                    int i = 0;
                    while (i < searchNodes.Count)
                    {
                        var node = searchNodes[i];
                        foreach (var childNode in node.Children)
                        {
                            searchNodes.Add(childNode);
                        }
                        if (node.AddonNode is AddonGroup)
                        {
                            searchNodes.RemoveAt(i);
                        }
                        else
                        {
                            node.Children = [];
                            i++;
                        }
                    }
                }

                IStringMatcher? stringMatcher;
                if (searchText.Length == 0)
                {
                    stringMatcher = null;
                }
                else if (options.IsRegex)
                {
                    stringMatcher = new RegexStringMatcher(searchText);
                }
                else
                {
                    stringMatcher = new DefaultStringMatcher(searchText, options.IgnoreCase);
                }

                ITagMatcher? tagMatcher;
                if (options.Tags == null)
                {
                    tagMatcher = null;
                }
                else
                {
                    tagMatcher = options.TagFilterMode switch
                    {
                        AddonTagFilterMode.Or => new OrModeTagMatcher(options.Tags),
                        AddonTagFilterMode.And => new AndModeTagMatcher(options.Tags),
                        AddonTagFilterMode.Not => new NotModeTagMatcher(options.Tags),
                        _ => null
                    };
                }

                foreach (var searchNode in searchNodes)
                {
                    if (searchNode.Match(stringMatcher, tagMatcher, cancellationToken))
                    {
                        consumer(searchNode.AddonNode);
                    }
                }
            }, cancellationToken);
        }

        private class SearchNode
        {
            public AddonNode AddonNode;
            public SearchNode[] Children = [];

            public string? Name = null;
            public HashSet<string>? Tags = null;

            private SearchNode(AddonNode addonNode, AddonNodeSearchOptions options)
            {
                AddonNode = addonNode;
                if (options.IncludeName)
                {
                    Name = addonNode.Name;
                }
                if (options.Tags != null)
                {
                    var tags = addonNode.TagsInHierarchy;
                    if (tags.Any())
                    {
                        Tags = [.. tags];
                    }
                }
            }

            public bool Match(IStringMatcher? stringMatcher, ITagMatcher? tagMatcher, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Children.Length == 0)
                {
                    return OnMatch(stringMatcher, tagMatcher);
                }

                var queue = new Queue<SearchNode>();
                queue.Enqueue(this);
                while (queue.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var node = queue.Dequeue();
                    if (node.OnMatch(stringMatcher, tagMatcher))
                    {
                        return true;
                    }
                    foreach (var childNode in node.Children)
                    {
                        queue.Enqueue(childNode);
                    }
                }

                return false;
            }

            private bool OnMatch(IStringMatcher? stringMatcher, ITagMatcher? tagMatcher)
            {
                return MatchTag(tagMatcher) && MatchString(stringMatcher);
            }

            private bool MatchString(IStringMatcher? matcher)
            {
                if (matcher == null)
                {
                    return true;
                }
                if (Name != null && matcher.Match(Name))
                {
                    return true;
                }
                return false;
            }

            private bool MatchTag(ITagMatcher? matcher)
            {
                if (matcher == null)
                {
                    return true;
                }
                return matcher.Match(Tags);
            }

            private class BuildStackFrame
            {
                public SearchNode SearchNode;
                public List<SearchNode> ChildSearchNodes = new();
                public IEnumerator<AddonNode> AddonNodeEnumerator;

                public BuildStackFrame(SearchNode searchNode, IEnumerator<AddonNode> addonNodeEnumerator)
                {
                    SearchNode = searchNode;
                    AddonNodeEnumerator = addonNodeEnumerator;
                }

                public BuildStackFrame(AddonGroup addonGroup, AddonNodeSearchOptions options)
                {
                    SearchNode = new SearchNode(addonGroup, options);
                    AddonNodeEnumerator = addonGroup.Children.GetEnumerator();
                }
            }

            public static SearchNode Build(AddonNode addonNode, AddonNodeSearchOptions options)
            {
                var addonGroup = addonNode as AddonGroup;
                if (addonGroup == null)
                {
                    return new SearchNode(addonNode, options);
                }

                var stack = new List<BuildStackFrame>();
                var firstStackFrame = new BuildStackFrame(addonGroup, options);
                stack.Add(firstStackFrame);

                while (stack.Count > 0)
                {
                    var frame = stack[stack.Count - 1];
                    if (frame.AddonNodeEnumerator.MoveNext())
                    {
                        var childAddonNode = frame.AddonNodeEnumerator.Current;
                        var childSearchNode = new SearchNode(childAddonNode, options);
                        frame.ChildSearchNodes.Add(childSearchNode);
                        if (childAddonNode is AddonGroup childAddonGroup)
                        {
                            stack.Add(new BuildStackFrame(childSearchNode, childAddonGroup.Children.GetEnumerator()));
                        }
                    }
                    else
                    {
                        frame.SearchNode.Children = frame.ChildSearchNodes.ToArray();
                        stack.RemoveAt(stack.Count - 1);
                    }
                }

                return firstStackFrame.SearchNode;
            }
        }

        private interface IStringMatcher
        {
            bool Match(string str);
        }

        private class DefaultStringMatcher : IStringMatcher
        {
            private string _targetStr;
            private StringComparison _stringComparison;

            public DefaultStringMatcher(string targetStr, bool ignoreCase)
            {
                _targetStr = targetStr;
                _stringComparison = ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;
            }

            public bool Match(string str)
            {
                return str.Contains(_targetStr, _stringComparison);
            }
        }

        private class RegexStringMatcher : IStringMatcher
        {
            private Regex? _regex = null;
            
            public RegexStringMatcher(string pattern)
            {
                try
                {
                    _regex = new(pattern);
                }
                catch (Exception ex)
                {
                    Log.Information(ex, "Exception occurred during creating Regex instance (pattern: {Pattern})", pattern);
                }
            }

            public bool Match(string str)
            {
                return _regex?.IsMatch(str) ?? false;
            }
        }

        private interface ITagMatcher
        {
            bool Match(ISet<string>? tags);
        }

        private class OrModeTagMatcher : ITagMatcher
        {
            private ISet<string> _requiredTags;

            public OrModeTagMatcher(ISet<string> requiredTags)
            {
                _requiredTags = requiredTags;
            }

            public bool Match(ISet<string>? tags)
            {
                if (tags == null)
                {
                    return _requiredTags.Count == 0;
                }
                foreach (var requiredTag in _requiredTags)
                {
                    if (tags.Contains(requiredTag))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private class AndModeTagMatcher : ITagMatcher
        {
            private ISet<string> _requiredTags;

            public AndModeTagMatcher(ISet<string> requiredTags)
            {
                _requiredTags = requiredTags;
            }

            public bool Match(ISet<string>? tags)
            {
                if (tags == null)
                {
                    return _requiredTags.Count == 0;
                }
                return _requiredTags.IsSubsetOf(tags);
            }
        }

        private class NotModeTagMatcher : ITagMatcher
        {
            private ISet<string> _requiredTags;

            public NotModeTagMatcher(ISet<string> requiredTags)
            {
                _requiredTags = requiredTags;
            }

            public bool Match(ISet<string>? tags)
            {
                if (tags == null)
                {
                    return true;
                }
                foreach (var requiredTag in _requiredTags)
                {
                    if (tags.Contains(requiredTag))
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }

    public class AddonNodeSearchOptions
    {
        public bool IgnoreCase { get; init; } = true;

        public bool IsFlatten { get; init; } = false;

        public bool IsRegex { get; init; } = false;

        public bool IncludeName { get; init; } = true;

        public ISet<string>? Tags { get; init; } = null;

        public AddonTagFilterMode TagFilterMode { get; init; } = AddonTagFilterMode.Or;
    }

    public enum AddonTagFilterMode
    {
        Or,
        And,
        Not
    }
}
