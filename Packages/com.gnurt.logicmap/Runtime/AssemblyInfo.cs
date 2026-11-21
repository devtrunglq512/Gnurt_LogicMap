using System.Runtime.CompilerServices;

// Assembly này chỉ chứa logic thuần (dữ liệu, xếp chart, đọc code), không gọi UnityEditor,
// nên test được dễ. Chỉ build cho Editor vì LogicMap không chạy trong game.
[assembly: InternalsVisibleTo("Gnurt.LogicMap.Editor")]
[assembly: InternalsVisibleTo("Gnurt.LogicMap.Tests.Editor")]
