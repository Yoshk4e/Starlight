using System.Text.Json.Serialization;

namespace Starlight.SDK.Http.Models;

/// <summary>
/// Payload returned inside <c>ApiResponse.Data</c> for
/// <c>/device-fp/api/getExtList</c>. Note that the success and error
/// variants use different field names for the message (<c>msg</c> vs
/// <c>message</c>).
/// </summary>
public sealed class DeviceExtListData
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("ext_list")]
    public List<string> ExtList { get; set; } = new();

    [JsonPropertyName("pkg_list")]
    public List<string> PkgList { get; set; } = new();

    [JsonPropertyName("pkg_str")]
    public string PkgStr { get; set; } = string.Empty;
}

/// <summary>
/// Payload returned inside <c>ApiResponse.Data</c> for
/// <c>/hk4e_global/combo/granter/api/compareProtocolVersion</c>.
/// </summary>
public sealed class CompareProtocolVersionData
{
    [JsonPropertyName("modified")]
    public bool Modified { get; set; }

    [JsonPropertyName("protocol")]
    public ProtocolInfo? Protocol { get; set; }
}

public sealed class ProtocolInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("app_id")]
    public int AppId { get; set; }

    [JsonPropertyName("language")]
    public required string Language { get; init; }

    [JsonPropertyName("user_proto")]
    public string UserProto { get; init; } = string.Empty;

    [JsonPropertyName("priv_proto")]
    public string PrivProto { get; init; } = string.Empty;

    [JsonPropertyName("major")]
    public int Major { get; set; }

    [JsonPropertyName("minimum")]
    public int Minimum { get; set; }

    [JsonPropertyName("create_time")]
    public string CreateTime { get; init; } = "0";

    [JsonPropertyName("teenager_proto")]
    public string TeenagerProto { get; init; } = string.Empty;

    [JsonPropertyName("third_proto")]
    public string ThirdProto { get; init; } = string.Empty;

    [JsonPropertyName("full_priv_proto")]
    public string FullPrivProto { get; init; } = string.Empty;
}

/// <summary>
/// Payload returned inside <c>ApiResponse.Data</c> for
/// <c>/hk4e_global/combo/granter/api/getConfig</c>.
/// </summary>
public sealed class ComboGranterConfigData
{
    [JsonPropertyName("protocol")]
    public bool Protocol { get; set; }

    [JsonPropertyName("qr_enabled")]
    public bool QrEnabled { get; set; }

    [JsonPropertyName("log_level")]
    public string LogLevel { get; set; } = "DEBUG";

    [JsonPropertyName("announce_url")]
    public string AnnounceUrl { get; set; } = string.Empty;

    [JsonPropertyName("push_alias_type")]
    public int PushAliasType { get; set; }

    [JsonPropertyName("disable_ysdk_guard")]
    public bool DisableYsdkGuard { get; set; }

    [JsonPropertyName("enable_announce_pic_popup")]
    public bool EnableAnnouncePicPopup { get; set; }

    [JsonPropertyName("app_name")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("qr_enabled_apps")]
    public Dictionary<string, bool>? QrEnabledApps { get; set; }

    [JsonPropertyName("qr_app_icons")]
    public Dictionary<string, string>? QrAppIcons { get; set; }

    [JsonPropertyName("qr_cloud_display_name")]
    public string QrCloudDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("enable_user_center")]
    public bool EnableUserCenter { get; set; }

    [JsonPropertyName("functional_switch_configs")]
    public Dictionary<string, bool> FunctionalSwitchConfigs { get; set; } = new();
}

/// <summary>
/// Payload returned inside <c>ApiResponse.Data</c> for
/// <c>/hk4e_global/mdk/shield/api/loadConfig</c>.
/// </summary>
public sealed class ShieldLoadConfigData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("game_key")]
    public required string GameKey { get; init; }

    [JsonPropertyName("app_id")]
    public int AppId { get; set; }

    [JsonPropertyName("client")]
    public required string Client { get; init; }

    [JsonPropertyName("identity")]
    public string Identity { get; init; } = "I_IDENTITY";

    [JsonPropertyName("guest")]
    public bool Guest { get; set; }

    [JsonPropertyName("ignore_versions")]
    public string IgnoreVersions { get; init; } = "2.6.0";

    [JsonPropertyName("scene")]
    public string Scene { get; init; } = "S_NORMAL";

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("disable_regist")]
    public bool DisableRegist { get; set; }

    [JsonPropertyName("enable_email_captcha")]
    public bool EnableEmailCaptcha { get; set; }

    [JsonPropertyName("thirdparty")]
    public List<string> Thirdparty { get; set; } = new();

    [JsonPropertyName("disable_mmt")]
    public bool DisableMmt { get; set; }

    [JsonPropertyName("server_guest")]
    public bool ServerGuest { get; set; }

    [JsonPropertyName("thirdparty_ignore")]
    public Dictionary<string, string> ThirdpartyIgnore { get; set; } = new();

    [JsonPropertyName("enable_ps_bind_account")]
    public bool EnablePsBindAccount { get; set; }

    [JsonPropertyName("thirdparty_login_configs")]
    public Dictionary<string, Dictionary<string, object>> ThirdpartyLoginConfigs { get; set; } = new();

    [JsonPropertyName("initialize_firebase")]
    public bool InitializeFirebase { get; set; }

    [JsonPropertyName("bbs_auth_login")]
    public bool BbsAuthLogin { get; set; }

    [JsonPropertyName("bbs_auth_login_ignore")]
    public List<string> BbsAuthLoginIgnore { get; set; } = new();

    [JsonPropertyName("fetch_instance_id")]
    public bool FetchInstanceId { get; set; }

    [JsonPropertyName("enable_flash_login")]
    public bool EnableFlashLogin { get; set; }

    [JsonPropertyName("enable_logo_18")]
    public bool EnableLogo18 { get; set; }

    [JsonPropertyName("logo_height")]
    public string LogoHeight { get; set; } = "0";

    [JsonPropertyName("logo_width")]
    public string LogoWidth { get; set; } = "0";

    [JsonPropertyName("enable_cx_bind_account")]
    public bool EnableCxBindAccount { get; set; }

    [JsonPropertyName("firebase_blacklist_devices_switch")]
    public bool FirebaseBlacklistDevicesSwitch { get; set; }

    [JsonPropertyName("firebase_blacklist_devices_version")]
    public int FirebaseBlacklistDevicesVersion { get; set; }

    [JsonPropertyName("hoyolab_auth_login")]
    public bool HoyolabAuthLogin { get; set; }

    [JsonPropertyName("hoyolab_auth_login_ignore")]
    public List<string> HoyolabAuthLoginIgnore { get; set; } = new();

    [JsonPropertyName("hoyoplay_auth_login")]
    public bool HoyoplayAuthLogin { get; set; }
}


public sealed class ComboBoxConfigData
{
    [JsonPropertyName("vals")]
    public Dictionary<string, string> Vals { get; set; } = new();
}

public sealed class ComboBoxPrecacheData
{
    [JsonPropertyName("data")]
    public ComboBoxPrecacheInner Data { get; set; } = new();
}

public sealed class ComboBoxPrecacheInner
{
    [JsonPropertyName("enable")]
    public string Enable { get; set; } = "false";

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Payload returned inside <c>ApiResponse.Data</c> for
/// <c>/hk4e_global/account/ma-passport/api/getConfig</c>.
/// </summary>
public sealed class MaPassportConfigData
{
    [JsonPropertyName("ip")]
    public MaPassportIpInfo Ip { get; set; } = new();

    [JsonPropertyName("area_wl")]
    public List<string> AreaWhitelist { get; set; } = new();

    [JsonPropertyName("realname_wl")]
    public List<string> RealnameWhitelist { get; set; } = new();

    [JsonPropertyName("guardian_age_limit")]
    public string GuardianAgeLimit { get; set; } = "14";

    [JsonPropertyName("disable_mmt")]
    public bool DisableMmt { get; set; }

    [JsonPropertyName("show_birthday")]
    public string ShowBirthday { get; set; } = "false";
}

public sealed class MaPassportIpInfo
{
    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en-us";

    [JsonPropertyName("area_code")]
    public string AreaCode { get; set; } = string.Empty;
}

/// <summary>
/// Body of <c>POST /data_abtest_api/config/experiment/list</c>.
/// </summary>
public sealed class ExperimentListRequest
{
    [JsonPropertyName("app_sign")]
    public string? AppSign { get; set; }

    [JsonPropertyName("scene_id")]
    public string? SceneId { get; set; }

    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("app_id")]
    public string? AppId { get; set; }
}

/// <summary>
/// Response wrapper for <c>/data_abtest_api/config/experiment/list</c>.
/// Unlike most other endpoints this one carries an extra
/// <see cref="Success"/> flag alongside the standard
/// <c>retcode/message/data</c> triple.
/// </summary>
public sealed class ExperimentListResponse
{
    [JsonPropertyName("retcode")]
    public int Retcode { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public List<ExperimentData> Data { get; init; } = new();
}

public sealed class ExperimentData
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("config_id")]
    public string ConfigId { get; set; } = string.Empty;

    [JsonPropertyName("period_id")]
    public string PeriodId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("configs")]
    public Dictionary<string, string> Configs { get; set; } = new();

    [JsonPropertyName("sceneWhiteList")]
    public bool SceneWhiteList { get; set; }

    [JsonPropertyName("experimentWhiteList")]
    public bool ExperimentWhiteList { get; set; }
}
