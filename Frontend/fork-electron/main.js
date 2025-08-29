const { app, BrowserWindow } = require('electron');
const axios = require('axios');

const BACKEND_URL = 'http://localhost:8080';
const CHECK_INTERVAL = 500; // ms between checks

function createWindow() {
  const mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  mainWindow.loadURL(BACKEND_URL);
  mainWindow.webContents.openDevTools(); // optional
}

// Poll backend until it's up
async function waitForBackend() {
  let backendReady = false;

  while (!backendReady) {
    try {
      await axios.get(BACKEND_URL);
      backendReady = true;
    } catch (err) {
      await new Promise(res => setTimeout(res, CHECK_INTERVAL));
    }
  }
}

app.whenReady().then(async () => {
  await waitForBackend();
  createWindow();
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) createWindow();
});
