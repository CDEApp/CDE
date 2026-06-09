// Prevents an additional console window on Windows in release.
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    tauri::Builder::default()
        // The frontend spawns/kills the bundled cdeApi sidecar via the shell plugin and reads its
        // stdout handshake (url + token); see src/sidecar.ts.
        .plugin(tauri_plugin_shell::init())
        // Native file/folder picker for "File > Open Folder…".
        .plugin(tauri_plugin_dialog::init())
        // Window geometry is Tauri-native (D14): persisted/restored by the window-state plugin,
        // not round-tripped through the C# /ui-state endpoint.
        .plugin(tauri_plugin_window_state::Builder::default().build())
        .run(tauri::generate_context!())
        .expect("error while running CDE tauri application");
}
