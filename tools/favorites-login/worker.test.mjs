import test from 'node:test';
import {readFile} from 'node:fs/promises';
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


test('Chromium retains cross-origin iframe state while its stable parent is hidden',async()=>{
  const browser=await chromium.launch({headless:true,chromiumSandbox:false});
  try{
    const page=await browser.newPage();
    let loads=0;
    await page.route('https://workspace.example.org/',route=>route.fulfill({
      contentType:'text/html',body:'<div id="host"><iframe title="retained" src="https://portal.example.org/"></iframe></div>'
    }));
    await page.route('https://portal.example.org/',route=>{
      loads++;
      return route.fulfill({contentType:'text/html',body:'<input id="draft"><div style="height:3000px"></div>'});
    });
    await page.goto('https://workspace.example.org/');
    const frame=page.frameLocator('iframe');
    await frame.locator('#draft').fill('unsaved draft');
    await frame.locator('body').evaluate(el=>el.ownerDocument.defaultView.scrollTo(0,600));
    await page.locator('#host').evaluate(el=>{el.hidden=true;el.inert=true;});
    await page.locator('#host').evaluate(el=>{el.hidden=false;el.inert=false;});
    assert.equal(await frame.locator('#draft').inputValue(),'unsaved draft');
    assert.equal(await frame.locator('body').evaluate(el=>el.ownerDocument.defaultView.scrollY),600);
    assert.equal(loads,1);
  }finally{await browser.close();}
});


// Run the actual production shim, not a duplicate test implementation.
test('wiki navigation keeps dynamic space links inside the session cookie path',{
  skip:!process.env.PLENARO_WIKI_SHIM_SOURCE
},async()=>{
  const source=await readFile(process.env.PLENARO_WIKI_SHIM_SOURCE,'utf8');
  const match=source.match(/private const string WikiNavigationShim = """\s*([\s\S]*?)\s*""";/);
  assert.ok(match);
  const script=match[1].replace('__WIKI_UPSTREAM__',JSON.stringify('https://wiki.example.org'));
  const browser=await chromium.launch({headless:true,chromiumSandbox:false});
  try{
    const context=await browser.newContext();
    await context.addCookies([{name:'Plenaro.FavoriteProxy',value:'mock-wiki-session',
      domain:'apps.example.org',path:'/wiki',secure:true,httpOnly:true,sameSite:'Strict'}]);
    const page=await context.newPage();
    await page.route('https://apps.example.org/**',async route=>{
      const path=new URL(route.request().url()).pathname;
      if(path==='/wiki/')return route.fulfill({contentType:'text/html',body:
        '<html><head>'+script+'</head><body><div id="menu"></div></body></html>'});
      const headers=await route.request().allHeaders();
      return route.fulfill({contentType:'text/html',body:
        path.startsWith('/wiki/')&&headers.cookie?.includes('mock-wiki-session')?
          '<h1>Wiki session retained</h1>':'<h1>Session missing</h1>'});
    });
    await page.goto('https://apps.example.org/wiki/');
    await page.locator('#menu').evaluate(node=>{
      node.innerHTML='<a id="space" href="/display/IT?x=1#section">Space</a>'+
        '<a id="absolute" href="https://wiki.example.org/pages/viewpage.action?pageId=42">Page</a>'+
        '<a id="existing" href="/wiki/display/IT">Existing</a>'+
        '<a id="foreign" href="https://other.example.org/display/IT">Foreign</a>'+
        '<a id="other" href="/site-00000000000040008000000000000001/">Other</a>';
    });
    await page.waitForFunction(()=>document.querySelector('#space').getAttribute('href')==='/wiki/display/IT?x=1#section');
    assert.equal(await page.locator('#absolute').getAttribute('href'),'/wiki/pages/viewpage.action?pageId=42');
    assert.equal(await page.locator('#existing').getAttribute('href'),'/wiki/display/IT');
    assert.equal(await page.locator('#foreign').getAttribute('href'),'https://other.example.org/display/IT');
    assert.equal(await page.locator('#other').getAttribute('href'),'/site-00000000000040008000000000000001/');
    await page.locator('#space').click();
    assert.equal(await page.locator('h1').textContent(),'Wiki session retained');
    assert.equal(page.url(),'https://apps.example.org/wiki/display/IT?x=1#section');
  }finally{await browser.close();}
});
