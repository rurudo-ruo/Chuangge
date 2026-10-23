# 窗格

**桌面整理与美化 | Organize and personalize your Windows desktop**

窗格 is a lightweight desktop organizer for grouping files, folders, and shortcuts into resizable, translucent desktop panels. Arrange items as thumbnails, sort them with drag-and-drop, and tune the appearance to suit your desktop and display scaling.

**欢迎提意见 | Contributions are welcome!**

This is a personal fork of [Twometer's NoFences](https://github.com/Twometer/NoFences), developed and maintained by **rurudo**. It is distributed under the MIT License. See [LICENSE](LICENSE) for the original copyright and license terms.

![窗格 desktop organizer](screenshot.png "窗格 in action")

## Features

- Create desktop panels to group files, folders, and shortcuts.
- View items as thumbnails and rearrange them by dragging.
- Move items between panels or add references by dragging files and folders from Explorer.
- Adjust global title appearance, hide panel titles, and fit all panels to their current thumbnail layout.
- Customize thumbnail, label, and spacing size independently of Windows display scaling.
- Work with missing items: locate replacements, remove entries, or open their containing folder.
- Scale across monitors with different DPI settings.

## Using 窗格

The notification-area icon provides **New fence**, **Advanced settings**, and **Exit** actions. Double-click the icon to open Advanced settings.

Open **Advanced settings** to set the global title size or hide all panel title bars and title text. Use **Fence content size** to scale thumbnails, labels, and spacing from 50% to 200%, independently of Windows display scaling. Existing panel dimensions are preserved; use **Modify all** to resize every panel to fit the new thumbnail layout. When titles are hidden, hold **Alt** while dragging a panel to move it; resize its edges as usual.

To launch the app when you sign in, enable **Start NoFences when I sign in** in Advanced settings and click **Apply**. Disabling the option removes the app's shortcut from your personal Windows Startup folder. This does not add a registry Run entry, scheduled task, or service.

Drag a thumbnail to reorder it within its panel, or onto another unlocked panel to move it. Dragging files and folders from Explorer adds references to them without moving the original files.

If an item is missing, it stays visible with a warning icon and a “(missing)” label. Right-click it to locate a replacement, remove it from the panel, or open its containing folder if that folder is still available.

## Downloads

GitHub releases provide an EXE-only download and a ZIP containing the app EXE and Microsoft's .NET Framework 4.8 web installer. The EXE-only download requires the runtime to already be installed. In the ZIP, run `ndp48-web.exe` first if .NET Framework 4.8 is missing, then run the app. The web installer downloads runtime components from Microsoft and requires an internet connection; it is not an offline runtime bundle.

## Build and development

For prerequisites, build instructions, project structure, and implementation notes, see [DEVELOPMENT.md](DEVELOPMENT.md).
