using System;
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
}
