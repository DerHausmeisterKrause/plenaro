import http from 'node:http';
import dns from 'node:dns/promises';
import net from 'node:net';
import {chromium} from 'playwright';
import {fileURLToPath} from 'node:url';

const PORT=3030;
const MAX_BODY=32*1024;
const MAX_CONCURRENT=2;
let active=0;

function json(res,status,body){
  res.writeHead(status,{
    'Content-Type':'application/json; charset=utf-8',
    'Cache-Control':'no-store',
    'X-Content-Type-Options':'nosniff'
  });
  res.end(JSON.stringify(body));
}

export function blockedIp(ip){
  if(net.isIP(ip)===4){
    const o=ip.split('.').map(Number);
    return o[0]===0 || o[0]===127 || o[0]>=224 ||
      (o[0]===169&&o[1]===254) || (o[0]===100&&o[1]===100) ||
      (o[0]===192&&o[1]===0&&o[2]===0);
  }
  if(net.isIP(ip)===6){
    const v=ip.toLowerCase();
    return v==='::'||v==='::1'||v.startsWith('fe8')||
      v.startsWith('fe9')||v.startsWith('fea')||v.startsWith('feb')||
      v.startsWith('ff')||v.startsWith('::ffff:127.')||
      v.startsWith('::ffff:169.254.');
  }
  return true;
}

async function safeTarget(value){
  if(typeof value!=='string'||value.length>2048)return null;
  let url;
  try{url=new URL(value);}catch{return null;}
  if(url.protocol!=='https:'||url.username||url.password||
     url.hash||url.hostname.length>253||url.pathname.startsWith('//'))
    return null;
  const addresses=await dns.lookup(url.hostname,{all:true,verbatim:true});
  if(addresses.length===0||addresses.some(x=>blockedIp(x.address)))
    return null;
  return url;
}

function validInput(body){
  if(!body||typeof body!=='object'||
     typeof body.username!=='string'||body.username.length<1||
     body.username.length>180||typeof body.password!=='string'||
     body.password.length<1||body.password.length>4096||
     !Array.isArray(body.cookies)||body.cookies.length>64)
    return false;
  return true;
}

async function visible(locator){
  try{return await locator.isVisible({timeout:600});}
  catch{return false;}
}

const userSelectors=[
  'input[placeholder="Benutzername"]',
  'input.z-focustextbox-real[type="text"][placeholder="Benutzername"]',
  'input[autocomplete="username"]','input[autocomplete="email"]',
  'input[type="email"]','input[name="j_username"]',
  'input[name="username"]','input[name="user"]','input[name="login"]',
  'input[id*="username" i]','input[id*="login" i]',
  'input[aria-label*="Benutzer" i]'
];
const pwdSelectors=[
  'input[placeholder="Kennwort"]',
  'input.z-focustextbox-real[type="password"]',
  'input[type="password"]','input[autocomplete="current-password"]',
  'input[name="j_password"]','input[name="password"]'
];

async function oneVisible(frame,selectors){
  for(const selector of selectors){
    const controls=frame.locator(selector);
    const count=Math.min(5,await controls.count());
    for(let i=0;i<count;i++){
      const item=controls.nth(i);
      if(await visible(item))return item;
    }
  }
  return null;
}

async function fields(page,expectedOrigin){
  for(const frame of page.frames()){
    if(frame.isDetached())continue;
    try{
      const here=new URL(frame.url());
      if(here.origin!==expectedOrigin)continue;
      const pass=await oneVisible(frame,pwdSelectors);
      if(!pass||await pass.getAttribute('autocomplete')==='new-password')
        continue;
      // Never submit registration or a password change.
      const passwordCount=await frame.locator('input[type="password"]:visible').count();
      if(passwordCount!==1)continue;
      const form=pass.locator('xpath=ancestor::form[1]');
      if(await form.count()){
        const action=(await form.getAttribute('action'))||'';
        if(/reset|register|forgot|change.?password/i.test(action))continue;
      }
      let user=await oneVisible(frame,userSelectors);
      if(!user){
        const inputs=frame.locator('input[type="text"]:visible,input[type="email"]:visible');
        if(await inputs.count()===1)user=inputs.first();
      }
      if(!user)continue;
      return {frame,user,pass,form:await form.count()?form:null};
    }catch{/* Dynamic frames may be disposed during navigation. */}
  }
  return null;
}

async function findSubmit(frame,form){
  const area=form??frame.locator('body');
  const controls=area.locator('button,input[type="submit"],input[type="button"],a.z-button,[role="button"],.z-button');
  const count=Math.min(await controls.count(),100);
  for(let i=0;i<count;i++){
    const el=controls.nth(i);
    if(!await visible(el)||await el.isDisabled())continue;
    const attrs=[
      await el.getAttribute('aria-label'),await el.getAttribute('title'),
      await el.getAttribute('value'),await el.textContent()
    ];
    const label=(attrs.find(x=>x&&x.trim())||'').trim();
    if(/^(anmelden|anmeldung|login|log in|sign in|einloggen)$/i.test(label))
      return el;
  }
  if(form){
    const submit=form.locator('button[type="submit"],input[type="submit"]');
    if(await submit.count()===1 && await visible(submit.first()))
      return submit.first();
  }
  return null;
}

export async function performLogin(page,origin,username,password){
  let found=null;
  const deadline=Date.now()+10000;
  while(Date.now()<deadline){
    found=await fields(page,origin);
    if(found)break;
    await page.waitForTimeout(250);
  }
  if(!found)return {ok:false,reason:'no-login-fields'};
  const initialUrl=page.url();
  await found.user.fill(username,{timeout:2500});
  await found.pass.fill(password,{timeout:2500});
  await found.pass.dispatchEvent('change');
  await found.pass.dispatchEvent('blur');

  const action=await findSubmit(found.frame,found.form);
  if(action){
    await action.click({timeout:3000});
  }else if(found.form){
    await found.pass.press('Enter',{timeout:1500});
  }else{
    // ATOSS custom ZK form without a recognized login action:
    // never guess an unrelated button or POST credentials elsewhere.
    return {ok:false,reason:'no-login-action'};
  }

  // Only a visibly completed same-origin login is considered successful.
  // Cookies alone are insufficient (an invalid-password login can set one).
  const confirmBy=Date.now()+11000;
  while(Date.now()<confirmBy){
    await page.waitForTimeout(400);
    if(!page.url().startsWith(origin+'/'))return {ok:false,reason:'foreign-origin'};
    if((await fields(page,origin))===null){
      await page.waitForTimeout(450);
      if((await fields(page,origin))===null){
        return {ok:true,reason:'login-fields-gone'};
      }
    }
  }
  return {ok:false,reason:'not-confirmed'};
}

export async function loginOnce(body){
  if(!validInput(body))return {authenticated:false,cookies:[],reason:'invalid-input'};
  const url=await safeTarget(body.url);
  if(!url)return {authenticated:false,cookies:[],reason:'blocked-target'};
  const origin=url.origin;
  const browser=await chromium.launch({
    headless:true,
    chromiumSandbox:false
  });
  try{
    const ctx=await browser.newContext({
      ignoreHTTPSErrors:false,serviceWorkers:'block'
    });
    try{
      // Only approved same-origin HTTPS gets network access. Browser web
      // content cannot target internal Docker services, metadata endpoints,
      // OAuth/SSO origins, or arbitrary hosts using stored credentials.
      await ctx.route('**/*',async route=>{
        const requestUrl=route.request().url();
        if(requestUrl.startsWith('data:')||requestUrl.startsWith('blob:'))
          return route.continue();
        try{
          if(new URL(requestUrl).origin===origin)
            return route.continue();
        }catch{/* refuse invalid URLs */}
        return route.abort('blockedbyclient');
      });
      const imported=body.cookies
        .filter(x=>x&&typeof x.name==='string'&&typeof x.value==='string'&&
          x.name.length<=256&&x.value.length<=4096&&
          typeof x.domain==='string'&&
          x.domain.replace(/^\./,'').toLowerCase()===url.hostname.toLowerCase())
        .map(x=>({name:x.name,value:x.value,url:origin,
          httpOnly:x.httpOnly===true,secure:true}));
      if(imported.length)await ctx.addCookies(imported);
      const page=await ctx.newPage();
      await page.goto(url.href,{waitUntil:'domcontentloaded',timeout:12500});
      const result=await performLogin(page,origin,body.username,body.password);
      if(!result.ok)return {authenticated:false,cookies:[],reason:result.reason};
      const cookies=(await ctx.cookies(origin))
        .filter(x=>x.secure&&x.domain.replace(/^\./,'').toLowerCase()===url.hostname.toLowerCase())
        .slice(0,64)
        .map(x=>({name:x.name,value:x.value,domain:x.domain,
          path:x.path,secure:true,httpOnly:x.httpOnly}));
      return {authenticated:cookies.length>0,cookies,
        reason:cookies.length>0?'confirmed':'no-session-cookies'};
    }finally{await ctx.close();}
  }finally{await browser.close();}
}

const server=http.createServer(async(req,res)=>{
  if(req.url==='/health'&&req.method==='GET'){
    res.writeHead(200,{'Content-Type':'text/plain','Cache-Control':'no-store'});
    return res.end('ready');
  }
  if(req.url!=='/login'||req.method!=='POST')
    return json(res,404,{authenticated:false});
  if(active>=MAX_CONCURRENT)
    return json(res,503,{authenticated:false,reason:'busy'});
  const header=Number(req.headers['content-length']||0);
  if(header>MAX_BODY)return json(res,413,{authenticated:false});
  const chunks=[];
  let size=0;
  try{
    for await(const chunk of req){
      size+=chunk.length;
      if(size>MAX_BODY)return json(res,413,{authenticated:false});
      chunks.push(chunk);
    }
    let body;
    try{body=JSON.parse(Buffer.concat(chunks).toString('utf8'));}
    catch{return json(res,400,{authenticated:false});}
    active++;
    try{
      const result=await loginOnce(body);
      return json(res,200,result);
    }finally{active--;}
  }catch{
    // No usernames, target URLs, cookies, passwords or browser traces in logs.
    return json(res,503,{authenticated:false,reason:'browser-unavailable'});
  }
});
server.requestTimeout=30000;
server.headersTimeout=10000;
server.listen(PORT,'0.0.0.0',()=>process.stdout.write('favorites-login ready\n'));
