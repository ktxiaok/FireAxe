using System;
using System.Reflection;

namespace L4D2AddonAssistant
{
    public static class AppGlobal
    {
        public const string GithubRepoLink = "https://github.com/ktxiaok/L4D2AddonAssistant";

        public const string License = "Apache-2.0 license";

        public static Version Version => Assembly.GetEntryAssembly()!.GetName().Version!;

        public static string VersionString => Version.ToString(3);
    }
}
