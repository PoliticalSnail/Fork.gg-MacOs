const { app, BrowserWindow } = require('electron');
const { spawn } = require('child_process');
const path = require('path');

let backendProcess;

function createWindow() {
  const mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  // Load your backend server URL that serves frontend + backend APIs
  mainWindow.loadURL('http://localhost:35565');
}

app.whenReady().then(() => {
  // Path to your published backend DLL
  const backendPath = path.join(__dirname, '..', 'Backend', 'publish', 'Fork.dll');

  // Start backend via dotnet
  backendProcess = spawn('dotnet', [backendPath], { stdio: 'inherit' });

  // Wait a few seconds to let backend start before loading window
  setTimeout(createWindow, 4000);
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

app.on('before-quit', () => {
  if (backendProcess) backendProcess.kill();
});
