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
