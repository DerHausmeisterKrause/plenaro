import {readFileSync} from 'node:fs';
import {Script} from 'node:vm';
import {expect,it} from 'vitest';

const csharp=readFileSync(new URL(
  '../../src/Plenaro.Api/Favorites/FavoriteProxy.Http.cs',import.meta.url),'utf8');
const embedded=csharp.match(
  /private const string BrowserPathShim = """\s*<script data-plenaro-browser-shim>([\s\S]*?)<\/script>\s*""";/
)?.[1];
if(!embedded)throw new Error('Favorite browser shim source not found in C#');

const slug='site-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';
const origin='https://apps-plenaro.example.org';
const upstream='https://atoss.example.org';

function makeShim(personal){
  return embedded
    .replaceAll('__PREFIX__',personal?'/'+slug:'/wiki')
    .replaceAll('__ALL_ROOT_PATHS__',String(personal))
    .replaceAll('__UPSTREAM_ORIGIN__',personal?JSON.stringify(upstream):'null');
}

it('compiles both embedded browser compatibility shims as real JavaScript',()=>{
  expect(()=>new Script(makeShim(true))).not.toThrow();
  expect(()=>new Script(makeShim(false))).not.toThrow();
});

it('rewrites dynamic personal images and login URLs without touching external origins',()=>{
  const requests=[];
  const fetches=[];
  const historyPaths=[];
  class ElementStub {
    constructor(tagName){this.tagName=tagName;this.nodeType=1;this.attrs=new Map();}
    getAttribute(name){return this.attrs.get(name)??null;}
    setAttribute(name,value){this.attrs.set(name,String(value));}
  }
  class ImageStub extends ElementStub {constructor(){super('IMG');}}
  class SourceStub extends ElementStub {constructor(){super('SOURCE');}}
  class FormStub extends ElementStub {constructor(){super('FORM');}}
  class ScriptStub extends ElementStub {constructor(){super('SCRIPT');}}
  class LinkStub extends ElementStub {constructor(){super('LINK');}}
  class NodeStub {
    appendChild(node){return node;}
    insertBefore(node){return node;}
  }
  for(const [proto,name] of [
    [ImageStub.prototype,'src'],[ImageStub.prototype,'srcset'],
    [SourceStub.prototype,'src'],[SourceStub.prototype,'srcset'],
    [FormStub.prototype,'action'],[ScriptStub.prototype,'src'],
    [LinkStub.prototype,'href']
  ]){
    Object.defineProperty(proto,name,{
      configurable:true,
      get(){return this.getAttribute(name);},
      set(value){this.setAttribute(name,value);}
    });
  }
  class XhrStub {
    open(method,url){requests.push([method,url]);}
  }
  class RequestStub {
    constructor(input,init){this.url=input;this.init=init;}
  }
  const window={
    location:{origin,href:origin+'/'+slug+'/login'},
    fetch:(url)=>{fetches.push(url);},
    Request:RequestStub
  };
  const history={
    pushState:(_state,_unused,url)=>historyPaths.push(url),
    replaceState:(_state,_unused,url)=>historyPaths.push(url)
  };
  const context={
    window,history,URL,
    XMLHttpRequest:XhrStub,Element:ElementStub,Node:NodeStub,
    HTMLImageElement:ImageStub,HTMLSourceElement:SourceStub,
    HTMLFormElement:FormStub,HTMLScriptElement:ScriptStub,
    HTMLLinkElement:LinkStub,
    navigator:{},document:{documentElement:null,addEventListener(){}}
  };
  new Script(makeShim(true)).runInNewContext(context);
  window.fetch('/api/login');
  window.fetch(upstream+'/api/account');
  window.fetch('https://cdn.example.net/public.png');
  const xhr=new XhrStub();
  xhr.open('POST','/login');
  const image=new ImageStub();
  image.src=upstream+'/images/logo.png';
  image.srcset='/images/1.png 1x, /images/2.png 2x';
  const source=new SourceStub();
  source.srcset='/images/small.webp 1x, /images/large.webp 2x';
  const form=new FormStub();
  form.action='/login/check';
  history.pushState({},'', '/dashboard');
  expect(fetches).toEqual([
    origin+'/'+slug+'/api/login',origin+'/'+slug+'/api/account',
    'https://cdn.example.net/public.png'
  ]);
  expect(requests).toEqual([['POST','/'+slug+'/login']]);
  expect(image.src).toBe('/'+slug+'/images/logo.png');
  expect(image.srcset).toBe(
    '/'+slug+'/images/1.png 1x, /'+slug+'/images/2.png 2x');
  expect(source.srcset).toContain('/'+slug+'/images/large.webp');
  expect(form.action).toBe('/'+slug+'/login/check');
  expect(historyPaths).toEqual(['/'+slug+'/dashboard']);
});
