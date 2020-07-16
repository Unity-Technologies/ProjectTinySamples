require('./weapp-adapter');

GameGlobal.WebAssembly = WXWebAssembly;

canvas.id = "UT_CANVAS";

GameGlobal.Module = {};
wx.getFileSystemManager().readFile({  
     filePath: 'TinyRacing.wasm.txt',  
      success: result => {    
           // 设置 wasm 数据    
            Module.wasm = result.data;     
            // 加载 wasm   
              require('./TinyRacing.js'); 
              }
             }); 
