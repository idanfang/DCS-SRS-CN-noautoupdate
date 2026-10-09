declare_plugin("DCS-SRS", {
	installed = true,
	dirName = current_mod_path,
	developerName = _("Ciribob"),
	developerLink = _("https://github.com/ciribob/DCS-SimpleRadioStandalone"),
	displayName = _("DCS SimpleRadio Standalone"),
	version = "2.4.1.0",
	state = "installed",
	info = _("DCS-SimpleRadio Standalone\n\n为 DCS 提供真实的无线电语音通信，并与各机型座舱集成。\n\n请在“特殊”设置中查看 SRS 集成选项。\n\n中文手动更新版：QQ群 1006786675\n原作者支持社区： https://discord.gg/baw7g3t"),
	binaries = {"srs.dll"},
    load_immediate = true,
	Skins = {
		{ name = "DCS-SRS", dir = "Theme" },
	},
	Options = {
		{ name = "DCS-SRS", nameId = "DCS-SRS", dir = "Options", allow_in_simulation = true; },
	},
})

plugin_done()