using System.Linq;
using NUnit.Framework;

namespace Gnurt.LogicMap.Tests
{
    public class FolderTreeTests
    {
        [Test]
        public void Build_FollowsFoldersAndMergesSingleChildChains()
        {
            var tree = FolderTree.Build(new[]
            {
                "Assets/Scenes/Menu.unity",
                "Assets/Scenes/Loading.unity",
                "Assets/_Game/Levels/World1/L1.unity",
                "Assets/_Game/Levels/World2/L2.unity",
                "Assets/_Game/Levels/World2/L3.unity",
                "Assets/Root.unity",
            });

            CollectionAssert.AreEqual(new[] { "Assets", "_Game/Levels", "Scenes" }, tree.Select(n => n.Label).ToArray());

            var loose = tree[0];
            CollectionAssert.AreEqual(new[] { "Assets/Root.unity" }, loose.Items);

            var levels = tree[1];
            Assert.AreEqual("Assets/_Game/Levels", levels.Path, "thư mục _Game chỉ có một thư mục con nên được gộp");
            Assert.AreEqual(3, levels.Total);
            Assert.IsEmpty(levels.Items);
            CollectionAssert.AreEqual(new[] { "World1", "World2" }, levels.Children.Select(c => c.Label).ToArray());
            Assert.AreEqual(2, levels.Children[1].Total);

            CollectionAssert.AreEqual(new[] { "Assets/Scenes/Loading.unity", "Assets/Scenes/Menu.unity" }, tree[2].Items, "file xếp theo tên");
        }

        [Test]
        public void Build_SortsNumbersByValueAndCountsEverything()
        {
            var tree = FolderTree.Build(new[] { "Assets/Maps/Map 10.unity", "Assets/Maps/Map 2.unity", "Assets/Maps/Map 1.unity" });
            var maps = tree.Single();
            CollectionAssert.AreEqual(new[] { "Map 1", "Map 2", "Map 10" }, maps.Items.Select(FolderTree.FileName).ToArray());
            Assert.AreEqual(3, maps.AllItems().Count());
        }

        [Test]
        public void FileName_DropsFolderAndExtension()
        {
            Assert.AreEqual("Map 1", FolderTree.FileName("Assets/Scenes/Map 1.unity"));
            Assert.AreEqual("Coin.v2", FolderTree.FileName("Assets/Prefabs/Coin.v2.prefab"));
        }
    }

    public class NameGroupsTests
    {
        [Test]
        public void Split_SkipsCommonPrefixAndGroupsByNextWord()
        {
            var names = new[] { "Item_Sword_01", "Item_Sword_02", "Item_Sword_03", "Item_Shield_01", "Item_Shield_02", "Item_Potion_01", "ItemBox" };
            var groups = NameGroups.Split(names);
            CollectionAssert.AreEqual(new[] { "Shield", "Sword", null }, groups.Select(g => g.Key).ToArray());
            Assert.AreEqual(3, groups[1].Names.Count);
            CollectionAssert.AreEquivalent(new[] { "Item_Potion_01", "ItemBox" }, groups[2].Names, "nhóm chỉ có một file gộp vào Khác");
        }

        [Test]
        public void Split_FallsBackToFirstLetter()
        {
            var groups = NameGroups.Split(new[] { "Apple", "Avocado", "Banana", "Cherry" });
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, groups.Select(g => g.Key).ToArray());
            Assert.AreEqual(2, groups[0].Names.Count);
        }

        [Test]
        public void Split_CutsLongUniformListsIntoRanges()
        {
            var names = Enumerable.Range(1, 130).Select(i => $"Tile_{i:000}").ToList();
            var groups = NameGroups.Split(names);
            CollectionAssert.AreEqual(new[] { 60, 60, 10 }, groups.Select(g => g.Names.Count).ToArray());
            Assert.AreEqual("Tile_001 … Tile_060", groups[0].Key);
        }

        [Test]
        public void Tokens_KeepCase()
        {
            CollectionAssert.AreEqual(new[] { "Item", "Fire", "Sword" }, NameRules.Tokens("Item_FireSword"));
            CollectionAssert.AreEqual(new[] { "item", "fire", "sword" }, NameRules.Words("Item_FireSword"));
        }
    }

    public class UsedByTests
    {
        [Test]
        public void UsedBy_ListsScenesAndPrefabsThatReferenceIt()
        {
            var scan = Fake.SceneGame();
            const string coin = "Assets/Prefabs/Coin.prefab";
            const string chest = "Assets/Prefabs/Chest.prefab";
            scan.sections[2].uses.Add(coin);
            scan.prefabs.Add(new AssetInfo { path = chest, name = "Chest", uses = { coin } });
            scan.prefabs.Add(new AssetInfo { path = coin, name = "Coin" });

            var analysis = new ProjectAnalysis(scan);
            CollectionAssert.AreEquivalent(new[] { "Assets/Scenes/Map 1.unity", chest }, analysis.UsedBy(coin).ToArray());
            Assert.IsEmpty(analysis.UsedBy(chest));
            Assert.AreEqual("Coin", analysis.Prefabs[coin].name);
            Assert.AreEqual("Map 1", analysis.Find("Assets/Scenes/Map 1.unity").name);
        }
    }
}
