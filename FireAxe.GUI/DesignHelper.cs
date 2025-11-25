using System;
using System.Text;
using System.Threading.Tasks;

namespace FireAxe;

internal static class DesignHelper
{
    public static AddonRoot CreateEmptyAddonRoot()
    {
        var root = new AddonRoot();
        root.TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        return root;
    }

    public static AddonNode CreateTestAddonNode()
    {
        var root = CreateEmptyAddonRoot();
        var node = AddonNode.Create<AddonNode>(root);
        node.Name = "test_node";
        return node;
    }

    public static AddonGroup CreateTestAddonGroup()
    {
        var root = CreateEmptyAddonRoot();
        var group = AddonNode.Create<AddonGroup>(root);
        group.Name = "test_group";
        return group;
    }

    public static AddonRoot CreateTestAddonRoot()
    {
        var addonRoot = CreateEmptyAddonRoot();
        AddTestAddonNodes(addonRoot);
        return addonRoot;
    }

    public static void AddTestAddonNodes(AddonRoot root)
    {
        var node1 = AddonNode.Create<AddonNode>(root);
        node1.Name = "node_1";

        var node2 = AddonNode.Create<AddonNode>(root);
        node2.Name = "node_2_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

        var node3 = AddonNode.Create<AddonGroup>(root);
        node3.Name = "node_3_group_single";
        node3.EnableStrategy = AddonGroupEnableStrategy.Single;

        var node4 = AddonNode.Create<AddonNode>(root, node3);
        node4.Name = "node_4";

        var node5 = AddonNode.Create<AddonGroup>(root, node3);
        node5.Name = "node_5_group_all";
        node5.EnableStrategy = AddonGroupEnableStrategy.All;
        
        var node6 = AddonNode.Create<AddonNode>(root, node5);
        node6.Name = "node_6";

        var node7 = AddonNode.Create<AddonNode>(root, node3);
        node7.Name = "node_7";

        var node8 = AddonNode.Create<AddonNode>(root, node5);
        node8.Name = "node_8";

        for (int i = 100; i <= 150; ++i)
        {
            var node = AddonNode.Create<AddonNode>(root);
            node.Name = "node_" + i;
        }
    }

    private static readonly char[] s_randomCharSource = [
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
        '随','机','生','成','汉','字','列','表','示','例','代','码','编','写','测','试','功','能','开','发','需','求','实','现','逻','辑','验','证','结','果',
    ];

    public static string GenerateRandomString(int length)
    {
        if (length < 1)
        {
            length = 1;
        }
        var builder = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            builder.Append(s_randomCharSource[Random.Shared.Next(s_randomCharSource.Length)]);
        }
        return builder.ToString();
    }

    public static string GenerateRandomString(int minLength, int maxLength)
    {
        if (minLength < 1 || maxLength < 1 || minLength > maxLength)
        {
            return GenerateRandomString(1);
        }

        return GenerateRandomString(Random.Shared.Next(minLength, maxLength + 1));
    }
}
