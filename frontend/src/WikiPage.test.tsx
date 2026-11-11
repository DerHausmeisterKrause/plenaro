import {StrictMode} from 'react';
import {afterEach,beforeEach,describe,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {MemoryRouter} from 'react-router-dom';
import {WikiPage} from './WikiPage';

const mocks=vi.hoisted(()=>({
  readWikiSettings:vi.fn(),startWiki:vi.fn()
}));
vi.mock('./wikiApi',async(importOriginal)=>{
  const real=await importOriginal<typeof import('./wikiApi')>();
  return {...real,readWikiSettings:mocks.readWikiSettings,
    startWiki:mocks.startWiki};
});
const ready={
  configured:true,name:'Firmenwiki',homePath:'/',username:'alice',
  hasPassword:true,ready:true,
  favoriteId:'00000000-0000-4000-8000-000000000010'
};
function renderPage(){return render(<StrictMode><MemoryRouter>
  <WikiPage/>
</MemoryRouter></StrictMode>);}
beforeEach(()=>{
  vi.clearAllMocks();
  mocks.readWikiSettings.mockResolvedValue(ready);
  mocks.startWiki.mockResolvedValue({
    url:'https://apps-plenaro.example.org/wiki/_plenaro/favorite-bootstrap?token=test'
  });
});
afterEach(cleanup);

describe('Central knowledge-base and mandatory user auto-login',()=>{
  it('opens only the backend-confirmed private Wiki session, once in StrictMode',async()=>{
    renderPage();
    const frame=await screen.findByTitle('Wissensdatenbank – Firmenwiki');
    expect((frame as HTMLIFrameElement).src)
      .toContain('/wiki/_plenaro/favorite-bootstrap?token=test');
    await waitFor(()=>expect(mocks.startWiki).toHaveBeenCalledTimes(1));
    expect(frame.getAttribute('sandbox')).toContain('allow-same-origin');
  });
  it('does not start when admin has not yet configured a Wiki',async()=>{
    mocks.readWikiSettings.mockResolvedValue({
      ...ready,configured:false,ready:false,favoriteId:null
    });
    renderPage();
    expect(await screen.findByText('Wiki noch nicht konfiguriert')).toBeTruthy();
    expect(mocks.startWiki).not.toHaveBeenCalled();
  });
  it('requires user-specific credentials before any Wiki iframe',async()=>{
    mocks.readWikiSettings.mockResolvedValue({
      ...ready,username:'',hasPassword:false,ready:false,favoriteId:null
    });
    renderPage();
    expect(await screen.findByText('Persönliche Wiki-Anmeldung einrichten')).toBeTruthy();
    expect(mocks.startWiki).not.toHaveBeenCalled();
  });
  it('rejects cross-app bootstrap destinations',async()=>{
    mocks.startWiki.mockResolvedValue({
      url:'https://apps-plenaro.example.org/cloud/_plenaro/favorite-bootstrap?token=x'
    });
    renderPage();
    expect(await screen.findByText('Ungültige Wiki-Proxy-Adresse.')).toBeTruthy();
    expect(screen.queryByTitle('Wissensdatenbank – Firmenwiki')).toBeNull();
  });
  it('shows a failed credential auto-login instead of silently opening Wiki',async()=>{
    mocks.startWiki.mockRejectedValue(new Error('Automatische Wiki-Anmeldung fehlgeschlagen.'));
    renderPage();
    expect(await screen.findByText('Automatische Wiki-Anmeldung fehlgeschlagen.')).toBeTruthy();
    expect(screen.queryByTitle('Wissensdatenbank – Firmenwiki')).toBeNull();
  });
  it('reconnects only on an explicit button click',async()=>{
    renderPage();
    const frame=await screen.findByTitle('Wissensdatenbank – Firmenwiki');
    fireEvent.load(frame);
    const reconnect=screen.getByRole('button',{name:/Wiki neu verbinden/});
    await waitFor(()=>expect(reconnect.hasAttribute('disabled')).toBe(false));
    fireEvent.click(reconnect);
    await waitFor(()=>expect(mocks.startWiki).toHaveBeenCalledTimes(2));
  });
});
