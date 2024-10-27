using System;

namespace L4D2AddonAssistant
{
    public static class DesignHelper
    {
        public static AddonNode CreateTestAddonNode()
        {
            var root = new AddonRoot();
            var node = new AddonNode(root);
            node.Name = "test_node";
            return node;
        }

        public static AddonGroup CreateTestAddonGroup()
        {
            var root = new AddonRoot();
            var group = new AddonGroup(root);
            group.Name = "test_group";
            return group;
        }

        public static AddonRoot CreateTestAddonRoot()
        {
            var addonRoot = new AddonRoot();
            AddTestAddonNodes(addonRoot);
            return addonRoot;
        }

        public static void AddTestAddonNodes(AddonRoot root)
        {
            AddonNode node1 = new(root);
            node1.Name = "node_1";

            AddonNode node2 = new(root);
            node2.Name = "node_2_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

            AddonGroup node3 = new(root);
            node3.Name = "node_3_group_single";
            node3.EnableStrategy = AddonGroupEnableStrategy.Single;

            AddonNode node4 = new(root, node3);
            node4.Name = "node_4";

            AddonGroup node5 = new(root, node3);
            node5.Name = "node_5_group_all";
            node5.EnableStrategy = AddonGroupEnableStrategy.All;
            
            AddonNode node6 = new(root, node5);
            node6.Name = "node_6";

            AddonNode node7 = new(root, node3);
            node7.Name = "node_7";

            AddonNode node8 = new(root, node5);
            node8.Name = "node_8";

            for (int i = 100; i <= 150; ++i)
            {
                var node = new AddonNode(root);
                node.Name = "node_" + i;
            }
        }
    }
}
