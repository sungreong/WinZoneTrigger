use serde_json::json;
use tauri::AppHandle;

// Verifies the packaged resource and the Rust -> Windows engine bridge without
// starting automation. Requires a caller-owned, isolated data directory.
pub fn run(app: AppHandle) {
    let result = (|| -> Result<(), String> {
        if std::env::var_os("WINZONE_TEST_DATA").is_none() {
            return Err("WINZONE_TEST_DATA is required for package verification".into());
        }
        let loaded = crate::bridge::request_sync(&app, json!({"Operation":"load"}))?;
        if !loaded["Config"]["Zones"].is_array() {
            return Err("Config response was invalid".into());
        }
        let zone = crate::bridge::request_sync(&app, json!({"Operation":"new-zone"}))?;
        if zone["Id"].as_str().unwrap_or_default().is_empty() {
            return Err("New zone response was invalid".into());
        }
        let state = crate::bridge::read_status();
        if state["Automation"]["ProcessId"] != 123 {
            return Err("Legacy BOM state was not readable".into());
        }
        Ok(())
    })();
    let exit = if result.is_ok() { 0 } else { 1 };
    if std::env::var_os("WINZONE_TEST_DATA").is_some() {
        if let Ok(path) = crate::bridge::config_dir() {
            let message = match result {
                Ok(()) => "PASS: Tauri resource + Rust/engine bridge + legacy state".into(),
                Err(e) => e,
            };
            let _ = std::fs::write(path.join("package-check.txt"), message);
        }
    }
    app.exit(exit);
}
