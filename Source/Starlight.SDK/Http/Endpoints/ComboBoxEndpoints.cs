using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Starlight.Common;
using Starlight.SDK.Common;
using Starlight.SDK.Http;
using Starlight.SDK.Http.Models;

namespace Starlight.SDK.Http.Endpoints;

/// <summary>
/// Implements the <c>/combo/box/api/config/sdk/combo</c> and
/// <c>/combo/box/api/config/sw/precache</c> endpoints.
/// </summary>
public static class ComboBoxEndpoints
{
    public static void MapComboBoxEndpoints(this IEndpointRouteBuilder routes)
    {
        foreach (var prefix in SdkRoutes.ComboBoxPathPrefixes)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                continue;

            routes.MapGet($"{prefix}/sdk/combo", HandleSdkCombo);
            routes.MapGet($"{prefix}/sw/precache", HandleSwPrecache);
        }
    }

    private static IResult HandleSdkCombo(
        [FromQuery] string? biz_key,
        [FromQuery] int? client_type,
        [FromServices] SdkConfig sdkConfig)
    {
        if (string.IsNullOrEmpty(biz_key) || client_type is null
            || !SdkUtils.IsValidGameBiz(biz_key))
        {
            return Results.Ok(ApiResponse.From(Retcode.ComboInvalidKey));
        }

        // client_type values 1..13 are accepted, except 7 (MacOS) and 12
        // (also MacOS variant).
        if (client_type is < 1 or > 13 or 7 or 12)
            return Results.Ok(ApiResponse.From(Retcode.ComboPlatformNoConfig));

        var shield = sdkConfig.Shield;
        var box = sdkConfig.ComboBox;
        var vals = new Dictionary<string, string>();

        // Per-client_type additions. We intentionally replicate the
        // fall-through behaviour of the upstream switch — case 2/8 falls
        // through into case 3/9, which then falls through into default.
        // C# disallows implicit fall-through, so we use explicit
        // `goto case` jumps to mirror the Java reference's behaviour.
        switch (client_type)
        {
            case 4:
            case 5:
                vals["modify_real_name_other_verify"] = Bool(shield.ModifyRealNameOtherVerify);
                vals["login_flow_notification"] = JsonBool("enable", shield.EnableLoginFlowNotification);
                break;
            case 11:
                vals["login_flow_notification"] = JsonBool("enable", shield.EnableLoginFlowNotification);
                break;
            case 10:
            case 13:
                vals["enable_telemetry_data_upload"] = Bool(box.EnableConsoleTelemetryUpload);
                vals["enable_telemetry_h5log"] = Bool(box.EnableTelemetryH5Log);
                vals["network_report_config"] = "{\n\t\"enable\": 1,\n\t\"status_codes\": [200]\n}";
                break;
            case 2:
            case 8:
                vals["alipay_recommend"] = Bool(box.EnableAliPayRecommend);
                vals["watermark_enable_web_notice"] = Bool(box.WatermarkEnableWebNotice);
                vals["enable_oaid"] = Bool(box.EnableOaid);
                vals["oaid_multi_process"] = Bool(box.EnableOaidMultiProcess);
                vals["oaid_call_hms"] = Bool(box.EnableOaidCallHms);
                vals["pay_platform_block_h5_cashier"] = Bool(box.EnablePayPlatformBlockH5Cashier);
                vals["pay_platform_h5_loading_limit"] = box.PayPlatformH5LoadingLimit.ToString();
                vals["isGooglePayV2"] = Bool(box.EnableGoogleV2);
                vals["report_black_list"] = "{\"key\":[\"download_update_progress\"]}";
                vals["bili_pay_cancel_strings"] = "[\"用户取消交易\"]\n";
                vals["enable_google_cancel_callback"] = Bool(box.EnableGoogleCancelCallback);
                goto case 3;
            case 3:
            case 9:
                vals["kcp_enable"] = Bool(box.EnableNewKcp);
                vals["kibana_pc_config"] = JsonSerializer.Serialize(new {
                    enable = box.EnableKibana ? 1 : 0,
                    level = Capitalize(shield.ApiLogLevel),
                    modules = box.KibanaModules
                });
                vals["webview_rendermethod_config"] = JsonSerializer.Serialize(new {
                    useLegacy = box.UseLegacyWebViewRenderMethod
                });
                vals["enable_web_dpi"] = Bool(box.EnableWebDpi);
                vals["account_list_page_enable"] = Bool(box.EnableAccountListPage);
                vals["new_forgotpwd_page_enable"] = Bool(box.EnableNewForgotPwdPage);
                vals["webview_apm_config"] = JsonSerializer.Serialize(new {
                    crash_capture_enable = box.EnableCrashCapture
                });
                vals["pay_payco_centered_host"] = box.PayCoCenteredHost;
                vals["payment_cn_config"] = JsonSerializer.Serialize(new {
                    h5_cashier_enable = box.EnableH5Cashier ? 1 : 0,
                    h5_cashier_timeout = box.H5CashierTimeout
                });
                goto default;
            default:
                vals["enable_telemetry_data_upload"] = Bool(box.EnableTelemetryDataUpload);
                vals["telemetry_config"] = JsonSerializer.Serialize(new {
                    dataupload_enable = box.EnableTelemetryDataUpload
                });
                vals["enable_apm_sdk"] = Bool(box.EnableApmSdk);
                vals["enable_telemetry_h5log"] = Bool(box.EnableTelemetryH5Log);
                vals["email_bind_remind"] = Bool(box.EnableEmailBindRemind);
                vals["email_bind_remind_interval"] = box.EmailBindRemindInterval.ToString();
                vals["enable_attribution"] = Bool(box.EnableAttribution);
                vals["disable_email_bind_skip"] = Bool(box.DisableEmailBindSkip);
                vals["new_register_page_enable"] = Bool(box.EnableNewRegisterPage);
                vals["h5log_config"] = JsonSerializer.Serialize(new {
                    enable = box.EnableH5Log,
                    level = Capitalize(shield.ApiLogLevel)
                });
                vals["appsflyer_config"] = JsonSerializer.Serialize(new {
                    enable = box.EnableAppsFlyer
                });
                vals["enable_register_autologin"] = Bool(box.EnableRegisterAutoLogin);
                vals["enable_user_center_v2"] = Bool(box.EnableUserCenterV2);
                vals["enable_twitter_v2"] = Bool(box.EnableTwitterV2);
                vals["modify_real_name_other_verify"] = Bool(shield.ModifyRealNameOtherVerify);
                vals["disable_try_verify"] = Bool(shield.DisableTryVerify);
                vals["login_flow_notification"] = JsonBool("enable", shield.EnableLoginFlowNotification);
                vals["network_report_config"] = JsonSerializer.Serialize(new {
                    enable = box.EnableNetworkReport ? 1 : 0,
                    status_codes = box.NetworkStatusCodes,
                    url_paths = box.NetworkUrlPaths
                });
                vals["enable_bind_google_sdk_order"] = Bool(box.EnableBindGoogleSdkOrder);
                vals["email_register_hide"] = Bool(shield.EnableRegisterHide);
                vals["httpdns_enable"] = Bool(box.EnableHttpDns);
                vals["httpdns_cache_expire_time"] = box.HttpDnsCacheExpireTime.ToString();
                vals["http_keep_alive_time"] = box.HttpKeepAliveTime.ToString();
                vals["list_price_tierv2_enable"] = Bool(box.EnableListPriceTierV2);
                vals["h5log_filter_config"] = JsonSerializer.Serialize(new {
                    function = new { event_name = box.NetworkConfigs }
                });
                break;
        }

        return Results.Ok(ApiResponse.Ok(new ComboBoxConfigData { Vals = vals }));
    }

    private static IResult HandleSwPrecache(
        [FromQuery] string? biz,
        [FromQuery] int? client,
        [FromServices] SdkConfig sdkConfig)
    {
        if (string.IsNullOrEmpty(biz) || client is null
            || !SdkUtils.IsValidGameBiz(biz))
        {
            return Results.Ok(ApiResponse.From(Retcode.ComboInvalidKey));
        }

        if (client is < 1 or > 3)
            return Results.Ok(ApiResponse.From(Retcode.ComboPlatformNoConfig));

        return Results.Ok(ApiResponse.Ok(new ComboBoxPrecacheData {
            Data = new ComboBoxPrecacheInner {
                Enable = sdkConfig.ComboBox.EnableServiceWorker.ToString().ToLowerInvariant(),
                Url = sdkConfig.ComboBox.ServiceWorkerUrl
            }
        }));
    }

    private static string Bool(bool value) => value.ToString().ToLowerInvariant();

    private static string JsonBool(string key, bool value)
        => JsonSerializer.Serialize(new Dictionary<string, object> { [key] = value });

    private static string Capitalize(string value)
        => string.IsNullOrEmpty(value) ? value
            : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}
