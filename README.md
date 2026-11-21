# Gnurt LogicMap

Tool Editor vẽ cấu trúc game thành flow chart để nhìn là hiểu: project chia theo scene hay theo prefab, có những map/minigame nào, script nào đưa người chơi từ đâu tới đâu. Chỉ để xem, không sửa logic game.

Mở bằng menu **Gnurt > LogicMap**. Unity tối thiểu 2022.3.

## Cài

Cài qua **Gnurt > Hub** (Hub cài kèm `com.gnurt.core`). Cài tay thì thêm cả hai vào `Packages/manifest.json`:

```json
"com.gnurt.core": "https://github.com/devtrunglq512/Gnurt_Hub.git?path=/Packages/com.gnurt.core#dev/0.1.0",
"com.gnurt.logicmap": "https://github.com/devtrunglq512/Gnurt_LogicMap.git?path=/Packages/com.gnurt.logicmap#dev/0.1.0"
```

## Repo này

- `Packages/com.gnurt.logicmap`: package (embedded trong project dev).
- `Assets/LogicMapDemo`: game demo nhỏ để thử (Loading → Menu → 3 map, khu minigame 3 prefab). Tạo lại bằng `-executeMethod LogicMapDemoBuilder.Build`.
- `Tools/compile_check.sh --tests`: compile và chạy EditMode test trên Unity 2022.3 và 6000.3 bằng project tạm.

Thay đổi theo version: [CHANGELOG](Packages/com.gnurt.logicmap/CHANGELOG.md).
