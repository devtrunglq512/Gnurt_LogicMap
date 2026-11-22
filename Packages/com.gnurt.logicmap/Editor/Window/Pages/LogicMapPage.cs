using Gnurt.Editor;

namespace Gnurt.LogicMap
{
    /// <summary>Trang của LogicMap: báo window khi được mở để sidebar dạng cây tô đúng mục.</summary>
    internal abstract class LogicMapPage : GnurtPage
    {
        public override void OnShow() => (Window as LogicMapWindow)?.OnPageShown(Id);
    }
}
