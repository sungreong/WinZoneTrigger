use serde_json::{json, Value};
use std::os::windows::process::CommandExt;
use std::{
    fs,
    io::{Read, Seek, SeekFrom},
    path::PathBuf,
    process::{Command, Stdio},
    thread,
    time::{Duration, Instant},
};
use tauri::{AppHandle, Manager};

pub fn engine(app: &AppHandle) -> Result<PathBuf, String> {
    let bundled = app
        .path()
        .resource_dir()
        .map_err(|e| e.to_string())?
        .join("WinZoneTrigger.Engine.exe");
    if bundled.is_file() {
        return Ok(bundled);
    }
    let development =
        PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../bin/WinZoneTrigger.Engine.exe");
    if cfg!(debug_assertions) && development.is_file() {
        return Ok(development);
    }
    Err("자동화 엔진이 없습니다. 설치 프로그램을 다시 실행하세요.".into())
}

pub fn command(app: &AppHandle) -> Result<Command, String> {
    let mut cmd = Command::new(engine(app)?);
    cmd.creation_flags(0x08000000)
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null());
    Ok(cmd)
}

pub fn start_engine(app: &AppHandle) -> Result<(), String> {
    command(app)?
        .arg("--minimized")
        .spawn()
        .map_err(|e| e.to_string())?;
    Ok(())
}

pub fn config_dir() -> Result<PathBuf, String> {
    if let Some(test_path) = std::env::var_os("WINZONE_TEST_DATA") {
        return Ok(PathBuf::from(test_path));
    }
    Ok(
        PathBuf::from(std::env::var_os("APPDATA").ok_or("APPDATA를 찾을 수 없습니다.")?)
            .join("WinZoneTrigger"),
    )
}

#[tauri::command]
pub async fn engine_request(app: AppHandle, request: Value) -> Result<Value, String> {
    tauri::async_runtime::spawn_blocking(move || request_sync(&app, request))
        .await
        .map_err(|e| e.to_string())?
}

pub(crate) fn request_sync(app: &AppHandle, request: Value) -> Result<Value, String> {
    let operation = request["Operation"].as_str().ok_or("작업이 없습니다.")?;
    if ![
        "load",
        "save",
        "scan",
        "new-zone",
        "apps",
        "pick-file",
        "startup",
    ]
    .contains(&operation)
    {
        return Err("허용되지 않은 작업입니다.".into());
    }
    let temp = tempfile::tempdir().map_err(|e| e.to_string())?;
    let input = temp.path().join("request.json");
    let output = temp.path().join("response.json");
    fs::write(
        &input,
        serde_json::to_vec(&request).map_err(|e| e.to_string())?,
    )
    .map_err(|e| e.to_string())?;
    let mut child = command(app)?
        .arg("--desktop-bridge")
        .arg(&input)
        .arg(&output)
        .spawn()
        .map_err(|e| e.to_string())?;
    let deadline =
        Instant::now() + Duration::from_secs(if operation == "pick-file" { 600 } else { 90 });
    loop {
        if child.try_wait().map_err(|e| e.to_string())?.is_some() {
            break;
        }
        if Instant::now() > deadline {
            let _ = child.kill();
            let _ = child.wait();
            return Err("작업 시간이 초과되었습니다. 다시 시도하세요.".into());
        }
        thread::sleep(Duration::from_millis(75));
    }
    let response: Value = serde_json::from_slice(&fs::read(output).map_err(|e| e.to_string())?)
        .map_err(|e| e.to_string())?;
    if response["Ok"] == true {
        // Starting an already running engine is a harmless no-op, protected by its named mutex.
        if operation == "save" {
            start_engine(app)?;
        }
        Ok(response["Result"].clone())
    } else {
        Err(response["Error"]
            .as_str()
            .unwrap_or("작업 실패")
            .to_string())
    }
}

fn read_json(name: &str) -> Value {
    config_dir()
        .ok()
        .and_then(|p| fs::read(p.join(name)).ok())
        .and_then(|v| {
            serde_json::from_slice(v.strip_prefix(&[0xef, 0xbb, 0xbf]).unwrap_or(&v)).ok()
        })
        .unwrap_or(Value::Null)
}

#[tauri::command]
pub fn read_status() -> Value {
    let logs = (|| -> std::io::Result<String> {
        let mut file = fs::File::open(
            config_dir()
                .map_err(std::io::Error::other)?
                .join("activity.log"),
        )?;
        let size = file.metadata()?.len();
        file.seek(SeekFrom::Start(size.saturating_sub(32_768)))?;
        let mut bytes = Vec::new();
        file.read_to_end(&mut bytes)?;
        let text = String::from_utf8_lossy(&bytes);
        let lines: Vec<_> = text.lines().rev().take(120).collect();
        Ok(lines.into_iter().rev().collect::<Vec<_>>().join("\n"))
    })()
    .unwrap_or_default();
    json!({ "Automation": read_json("automation-state.json"), "Wifi": read_json("wifi-state.json"), "Logs": logs })
}

#[tauri::command]
pub fn open_config_folder() -> Result<(), String> {
    let path = config_dir()?;
    fs::create_dir_all(&path).map_err(|e| e.to_string())?;
    Command::new("explorer.exe")
        .arg(path)
        .spawn()
        .map_err(|e| e.to_string())?;
    Ok(())
}

#[tauri::command]
pub fn open_legacy(app: AppHandle) -> Result<(), String> {
    command(&app)?
        .arg("--legacy-ui")
        .spawn()
        .map_err(|e| e.to_string())?;
    Ok(())
}
