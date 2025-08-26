const { app, BrowserWindow, ipcMain } = require('electron');
const path = require('path');

let mainWindow;

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 800,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'), // optional
      nodeIntegration: true,
      contextIsolation: false, // set to true if using preload.js securely
    },
  });

  mainWindow.loadFile('index.html'); // Or your frontend entry (e.g. dist/index.html)

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

// App lifecycle
app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
  // macOS convention to keep app active until Cmd+Q
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', () => {
  // macOS: recreate window if dock icon clicked and no windows open
  if (mainWindow === null) {
    createWindow();
  }
});

// Optional: IPC handlers to talk to backend logic
ipcMain.handle('download-server-jar', async (event, url, savePath) => {
  const fs = require('fs');
  const axios = require('axios');

  const writer = fs.createWriteStream(savePath);
  const response = await axios({
    method: 'get',
    url,
    responseType: 'stream'
  });

  response.data.pipe(writer);

  return new Promise((resolve, reject) => {
    writer.on('finish', resolve);
    writer.on('error', reject);
  });
});
