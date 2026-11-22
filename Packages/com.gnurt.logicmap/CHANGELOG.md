# Changelog

## [0.1.0] - Unreleased

Bản đầu tiên: quét project và vẽ cấu trúc game thành flow chart, chỉ để xem (không sửa được logic game).

### Editor (`Gnurt > LogicMap`)
- **Tổng quan**: project chia theo scene hay theo prefab, số scene trong build, số prefab và bao nhiêu là map/minigame, số lệnh chuyển scene. Hai khối riêng **Scene** (scene trong build theo thứ tự) và **Prefab** (prefab là map/minigame), mỗi khối có các ô thư mục để đi thẳng vào.
- **Luồng game**: flow chart cả game, từ scene đầu trong Build Settings tới các scene/prefab. Các map có cùng đầu vào và đầu ra được gộp thành một ô.
- **Sidebar dạng cây chia hai mục lớn SCENE và PREFAB**, theo đúng cây thư mục của project (thư mục trung gian chỉ có một thư mục con được gộp, vd `_Game/Prefabs`; nhớ thư mục nào đang mở). Scene và prefab là map/minigame hiện thẳng trên cây; prefab thường (item, UI…) chỉ hiện thư mục kèm số lượng.
- **Trang thư mục**: ô Tìm, thư mục con, các scene/prefab bên trong; thư mục từ 24 file trở lên được chia nhóm theo tên (bỏ phần đầu chung như `Item_`, nhóm theo từ tiếp theo: Sword, Shield…; không theo quy luật thì theo chữ cái đầu hoặc theo khoảng), nhóm gập sẵn khi quá đông. Bấm một file để xem nó dùng script nào, chứa prefab nào, được scene/prefab nào dùng.
- **Trang từng scene / map/minigame**: từ đâu tới, hàm nào trong script chuyển đi đâu (hàm có nhiều đích vẽ thành hình thoi kèm điều kiện if/else/case), prefab nào được chứa; danh sách script của mục.
- **Script dùng chung**: script có `DontDestroyOnLoad`, script gắn ở nhiều mục, class không gắn vào scene nhưng có chuyển scene.
- Chart tự xếp theo tầng, kéo nền để di chuyển, lăn chuột để phóng to, `F` để vừa khung, bấm đúp ô để mở trang; panel chi tiết mở scene/prefab/script đúng dòng.
- **Hỏi Claude**: kiểm tra máy có Claude Code CLI (`claude --version`) và báo trạng thái; nút **Cài tự động** chạy trình cài native chính thức của Anthropic ở nền (hỏi trước khi chạy, hiện output, báo lỗi), cài xong tự kiểm tra lại. Ô chat bị khoá ở bản này.
- **Cài đặt** (lưu ở `Assets/GnurtLogicMap/GnurtLogicMapSettings.asset`): thư mục prefab-mục (tự tìm thư mục tên kiểu Maps, Levels, MiniGames), thư mục bỏ qua, hàm chuyển scene tự viết. Đường dẫn Claude CLI lưu riêng từng người.

### Cách quét
- Scene trong Build Settings và mọi scene khác trong Assets; mọi prefab trong Assets (bỏ thư mục trong mục "bỏ qua").
- Script của từng mục lấy qua `AssetDatabase.GetDependencies` (tính cả prefab lồng).
- Đọc code C# bằng regex (không compile): `LoadScene`/`LoadSceneAsync` với tên, số thứ tự, hằng chuỗi, `nameof`, ghép chuỗi (`"Map " + i` → `Map *`), `buildIndex + 1`, tải lại scene hiện tại. Tham số tính lúc chạy thì ghi là "Tính lúc chạy".
- Kết quả lưu ở `Library/Gnurt/LogicMap/Scan.json`.

### Kỹ thuật
- `Gnurt.LogicMap`: logic thuần (mô hình chart, xếp chart, đọc code, phân tích), chỉ build cho Editor.
- `Gnurt.LogicMap.Editor`: window, trang, chart. Cả hai chỉ compile khi project có `com.gnurt.core` (`GNURT_CORE`).
