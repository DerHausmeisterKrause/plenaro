import test from 'node:test';
import assert from 'node:assert/strict';
import {chromium} from 'playwright';
import {blockedIp,performLogin} from './server.mjs';

test('browser worker refuses loopback, metadata and link-local routes',()=>{
  for(const address of ['127.0.0.1','169.254.169.254','100.100.100.200',
    '::1','fe80::abcd','ff02::1'])
    assert.equal(blockedIp(address),true,address);
  for(const address of ['192.168.178.70','10.28.11.13','8.8.8.8','fd00::1'])
    assert.equal(blockedIp(address),false,address);
});

test('actual Chromium fills dynamically created ATOSS/ZK fields and gets an HTTPS session cookie',async()=>{
  const browser=await chromium.launch({headless:true,chromiumSandbox:false});
  try{
    const context=await browser.newContext();
    try{
      await context.route('https://atoss.example.org/login',route=>
        route.fulfill({status:200,contentType:'text/html',body:`
<!doctype html><html><body><main id="app">Loading ATOSS...</main>
<script>
setTimeout(()=>{
  document.querySelector('#app').innerHTML =
  '<input type="text" class="z-focustextbox-real" placeholder="Benutzername">'+
  '<input type="password" class="z-focustextbox-real" placeholder="Kennwort">'+
  '<a class="z-button" role="button">Anmelden</a>';
  document.querySelector('a.z-button').addEventListener('click',()=>{
    if(document.querySelector('input[type=text]').value==='test.user' &&
       document.querySelector('input[type=password]').value==='test.password'){
      document.cookie='ATOSS_SESSION=mocked-login; Secure; Path=/';
      document.querySelector('#app').innerHTML='<h1>ATOSS angemeldet</h1>';
    }
  });
},200);
</script></body></html>`}));
      const page=await context.newPage();
      await page.goto('https://atoss.example.org/login');
      const outcome=await performLogin(page,'https://atoss.example.org',
        'test.user','test.password');
      assert.equal(outcome.ok,true,outcome.reason);
      assert.equal(await page.locator('h1').textContent(),'ATOSS angemeldet');
      const cookies=await context.cookies('https://atoss.example.org');
      assert.ok(cookies.some(x=>x.name==='ATOSS_SESSION'&&
        x.value==='mocked-login'&&x.secure));
    }finally{await context.close();}
  }finally{await browser.close();}
});

test('actual Chromium never clicks unrelated password reset buttons',async()=>{
  const browser=await chromium.launch({headless:true,chromiumSandbox:false});
  try{
    const context=await browser.newContext();
    try{
      await context.route('https://atoss.example.org/login',route=>
        route.fulfill({status:200,contentType:'text/html',body:`
<!doctype html><html><body>
<input type="text" placeholder="Benutzername">
<input type="password" placeholder="Kennwort">
<a class="z-button" role="button" onclick="window.resetClicked=true">
  Passwort vergessen
</a>
</body></html>`}));
      const page=await context.newPage();
      await page.goto('https://atoss.example.org/login');
      const outcome=await performLogin(page,'https://atoss.example.org',
        'test.user','test.password');
      assert.equal(outcome.ok,false);
      assert.equal(outcome.reason,'no-login-action');
      assert.equal(await page.evaluate(()=>window.resetClicked),undefined);
    }finally{await context.close();}
  }finally{await browser.close();}
});


// Actual Chromium decoding check: the attachment preview must not rely on
// an img-src policy permitting blob: URLs. No real attachment leaves the app.
test('Chromium decodes PNG bytes into canvas even with restricted image CSP',async()=>{
  const browser=await chromium.launch({headless:true,chromiumSandbox:false});
  try{
    const page=await browser.newPage();
    await page.route('https://preview.example.org/',route=>route.fulfill({
      status:200,contentType:'text/html',
      headers:{'Content-Security-Policy':"default-src 'none'; img-src 'self'"},
      body:'<!doctype html><canvas id="output"></canvas>'
    }));
    await page.goto('https://preview.example.org/');
    const pixels=await page.evaluate(async()=>{
      const source=document.createElement('canvas');
      source.width=2;source.height=1;
      const ctx=source.getContext('2d');
      ctx.fillStyle='#ff0000';ctx.fillRect(0,0,2,1);
      const png=await new Promise(resolve=>source.toBlob(resolve,'image/png'));
      const bitmap=await createImageBitmap(png);
      const output=document.getElementById('output');
      output.width=bitmap.width;output.height=bitmap.height;
      const target=output.getContext('2d');
      target.drawImage(bitmap,0,0);
      bitmap.close();
      return Array.from(target.getImageData(0,0,1,1).data);
    });
    assert.deepEqual(pixels,[255,0,0,255]);
  }finally{await browser.close();}
});
