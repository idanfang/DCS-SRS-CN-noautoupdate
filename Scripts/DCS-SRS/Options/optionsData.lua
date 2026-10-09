--package.cpath = package.cpath..";"..lfs.writedir().."Mods\\tech\\DCS-SRS\\bin\\?.dll;"

cdata = {
  DCS_SRS = _("DCS-SRS 设置"),
  DCS_SRS_CLIENT_PATH_LABEL = _("DCS-SRS 客户端路径"),
  DCS_SRS_CLIENT_PATH = _("请在 SRS 设置中点击“为 DCS 设置 SRS 路径”。"),
  DCS_SRS_OVERLAY = _("默认显示 DCS 中的 SRS 浮窗"),
  DCS_SRS_OVERLAY_INFO = _("勾选后默认显示浮窗；未勾选时，可按左 Ctrl + 左 Shift + Esc 显示。"),
  DCS_SRS_AUTO_LAUNCH = _("自动启动 SRS"),
  DCS_SRS_AUTO_LAUNCH_INFO = _("勾选后，连接启用 SRS 自动连接的服务器时会自动启动 SRS。"),
  DCS_SRS_AUTO_LAUNCH_INFO_2 = _("若 SRS 无法启动，请先在 SRS 设置中点击“为 DCS 设置 SRS 路径”。"),
  DCS_SRS_OVERLAY_COMPACT = _("DCS 中的 SRS 浮窗精简模式"),
  DCS_SRS_OVERLAY_COMPACT_INFO = _("服务器和客户端均启用“显示发送者名称”时，以发送者名称替换频率显示，而不是追加显示。"),
  DCS_SRS_OVERLAY_HELP_TEXT = _("显示 SRS 浮窗帮助文字"),
  DCS_SRS_OVERLAY_HELP_TEXT_INFO = _("勾选后，在 DCS 浮窗中显示完整的 SRS 帮助文字。")
}
