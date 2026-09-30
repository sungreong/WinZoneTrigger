#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]
mod bridge;
mod package_check;
use tauri::{
    menu::{Menu, MenuItem},
    tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent},
    Manager,
};

fn show(app: &tauri::AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.show();
        let _ = window.unminimize();
        let _ = window.set_focus();
    }
}

fn main() {
    // Isolated package verification must not be forwarded to a user's running app.
    let verify_package = std::env::args().any(|a| a == "--verify-package")
        && std::env::var_os("WINZONE_TEST_DATA").is_some();
    let builder = tauri::Builder::default();
    let builder = if verify_package {
        builder
    } else {
        builder.plugin(tauri_plugin_single_instance::init(|app, args, _| {
            if !args.iter().any(|a| a == "--minimized") {
                show(app);
            }
        }))
    };
    builder
        .invoke_handler(tauri::generate_handler![
            bridge::engine_request,
            bridge::read_status,
            bridge::open_config_folder,
            bridge::open_legacy
        ])
        .setup(|app| {
            if std::env::args().any(|a| a == "--verify-package") {
                let handle = app.handle().clone();
                std::thread::spawn(move || package_check::run(handle));
                return Ok(());
            }
            let open = MenuItem::with_id(app, "open", "설정 화면 열기", true, None::<&str>)?;
            let quit = MenuItem::with_id(
                app,
                "quit",
                "화면 앱 종료 (자동화 유지)",
                true,
                None::<&str>,
            )?;
            let menu = Menu::with_items(app, &[&open, &quit])?;
            TrayIconBuilder::new()
                .icon(app.default_window_icon().unwrap().clone())
                .tooltip("WinZoneTrigger · 위치별 자동화")
                .menu(&menu)
                .show_menu_on_left_click(false)
                .on_menu_event(|app, event| match event.id.as_ref() {
                    "open" => show(app),
                    "quit" => app.exit(0),
                    _ => {}
                })
                .on_tray_icon_event(|tray, event| {
                    if matches!(
                        event,
                        TrayIconEvent::Click {
                            button: MouseButton::Left,
                            button_state: MouseButtonState::Up,
                            ..
                        }
                    ) {
                        show(tray.app_handle());
                    }
                })
                .build(app)?;
            bridge::start_engine(app.handle()).map_err(std::io::Error::other)?;
            if !std::env::args().any(|a| a == "--minimized") {
                show(app.handle());
            }
            Ok(())
        })
        .on_window_event(|window, event| {
            if let tauri::WindowEvent::CloseRequested { api, .. } = event {
                api.prevent_close();
                let _ = window.hide();
            }
        })
        .run(tauri::generate_context!())
        .expect("WinZoneTrigger 시작 실패");
}
