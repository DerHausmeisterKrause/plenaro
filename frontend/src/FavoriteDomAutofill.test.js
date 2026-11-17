import {readFileSync} from 'node:fs';
import {Script} from 'node:vm';
import {JSDOM} from 'jsdom';
import {describe,expect,it,vi} from 'vitest';

const source=readFileSync(new URL(
  '../../src/Plenaro.Api/Favorites/FavoriteDomAutoLogin.cs',
  import.meta.url),'utf8');
const match=source.match(
  /private const string AutoFillScript = """\s*(<script data-plenaro-dom-autofill>[\s\S]*?<\/script>)\s*""";/);
if(!match)throw new Error('Shipped dedicated autofill template not found');
const js=match[1].replace(/^\s*<script[^>]*>/,'')
  .replace(/<\/script>\s*$/,'');
const slug='site-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';
const host=slug+'-plenaro.example.org';
const nonce='0123456789ABCDEF0123456789ABCDEF';

function renderScript(h=host,secret='test-password'){
  return js
    .replaceAll('__HOST__',JSON.stringify(host))
    .replaceAll('__USERNAME__',JSON.stringify('test.user'))
    .replaceAll('__PASSWORD__',JSON.stringify(secret))
    .replaceAll('__NONCE__',JSON.stringify(nonce));
}

function page(markup,origin=host){
  const dom=new JSDOM(
    '<!doctype html><html><head></head><body>'+markup+'</body></html>',
    {url:'https://'+origin+'/login',runScripts:'outside-only'});
  const {window}=dom;
  const scheduled=[];
  window.setTimeout=cb=>{scheduled.push(cb);return scheduled.length;};
  window.MutationObserver=undefined;
  window.HTMLElement.prototype.getClientRects=function(){
    return [Object.freeze({width:100,height:20})];
  };
  return {window,scheduled,run(){window.eval(renderScript());}};
}

describe('Dedicated personal favorite DOM autofill (WPF parity)',()=>{
  it('shipped JavaScript compiles without alteration',()=>{
    expect(()=>new Script(renderScript())).not.toThrow();
  });

  it('detects ATOSS ZK input selectors, uses native setter and DOM events, submits once',()=>{
    const p=page(
      '<form id="login">'+
      '<input name="employee" class="z-focustextbox-real" type="text" placeholder="Benutzername">'+
      '<input name="secret" class="z-focustextbox-real" type="password" placeholder="Kennwort">'+
      '<button type="submit">Anmelden</button></form>');
    const form=p.window.document.querySelector('form');
    const user=form.querySelector('[name="employee"]');
    const pwd=form.querySelector('[name="secret"]');
    const values=[];
    user.addEventListener('input',()=>values.push('user-input'));
    user.addEventListener('change',()=>values.push('user-change'));
    pwd.addEventListener('input',()=>values.push('password-input'));
    pwd.addEventListener('change',()=>values.push('password-change'));
    form.requestSubmit=vi.fn();
    p.run();
    expect(p.scheduled.length).toBeGreaterThan(3);
    p.scheduled[0]();
    expect(user.value).toBe('test.user');
    expect(pwd.value).toBe('test-password');
    expect(values).toEqual(['user-input','user-change',
      'password-input','password-change']);
    expect(form.requestSubmit).toHaveBeenCalledTimes(1);
    for(const fn of p.scheduled)fn();
    expect(form.requestSubmit).toHaveBeenCalledTimes(1);
    expect(p.window.sessionStorage.getItem('plenaro:autofill:'+nonce))
      .toBe('submitted');
    p.window.close();
  });

  it('finds dynamic login fields when JS creates them after initial HTML',()=>{
    const p=page('<main id="app"></main>');
    p.run();
    p.scheduled[0]();
    const form=p.window.document.createElement('form');
    form.innerHTML=
      '<input name="j_username" type="text">'+
      '<input name="j_password" type="password">'+
      '<button type="submit">Login</button>';
    form.requestSubmit=vi.fn();
    p.window.document.querySelector('main').appendChild(form);
    p.scheduled[1]();
    expect(form.querySelector('[name="j_username"]').value).toBe('test.user');
    expect(form.querySelector('[name="j_password"]').value).toBe('test-password');
    expect(form.requestSubmit).toHaveBeenCalledTimes(1);
    p.window.close();
  });

  it('also fills login fields in an accessible nested about:blank frame',()=>{
    const p=page('<iframe id="embedded"></iframe>');
    const iframe=p.window.document.querySelector('iframe');
    const inner=iframe.contentDocument;
    expect(inner).not.toBeNull();
    inner.body.innerHTML=
      '<form><input type="text" name="user" placeholder="Benutzername">'+
      '<input type="password" name="pw" placeholder="Kennwort">'+
      '<button type="submit">Anmelden</button></form>';
    iframe.contentWindow.HTMLElement.prototype.getClientRects=function(){
      return [Object.freeze({width:100,height:20})];
    };
    const form=inner.querySelector('form');
    form.requestSubmit=vi.fn();
    p.run();
    p.scheduled[0]();
    expect(inner.querySelector('[name="user"]').value).toBe('test.user');
    expect(inner.querySelector('[name="pw"]').value).toBe('test-password');
    expect(form.requestSubmit).toHaveBeenCalledTimes(1);
    p.window.close();
  });

  it('never fills or submits on another origin',()=>{
    const p=page('<form><input name="user"><input type="password"></form>',
      'apps-plenaro.example.org');
    const form=p.window.document.querySelector('form');
    form.requestSubmit=vi.fn();
    p.run();
    expect(p.scheduled).toHaveLength(0);
    expect(form.requestSubmit).not.toHaveBeenCalled();
    expect(form.querySelector('[name="user"]').value).toBe('');
    p.window.close();
  });

  it('does not overwrite user input or submit change-password dialogs',()=>{
    const p=page('<form action="/password/change">'+
      '<input name="username" value="different-user">'+
      '<input type="password" name="current" autocomplete="current-password">'+
      '<input type="password" name="new" autocomplete="new-password">'+
      '<button type="submit">Kennwort ändern</button></form>');
    const form=p.window.document.querySelector('form');
    form.requestSubmit=vi.fn();
    p.run();
    p.scheduled[0]();
    expect(form.requestSubmit).not.toHaveBeenCalled();
    expect(form.querySelector('[name="current"]').value).toBe('');
    expect(form.querySelector('[name="username"]').value).toBe('different-user');
    p.window.close();
  });
});
