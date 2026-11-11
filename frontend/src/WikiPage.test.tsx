import {StrictMode} from 'react';
import {afterEach,beforeEach,describe,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {MemoryRouter} from 'react-router-dom';
import {WikiPage} from './WikiPage';

const mocks=vi.hoisted(()=>({
  loadFavorites:vi.fn(),startFavorite:vi.fn(),
  readWikiSelection:vi.fn(),saveWikiSelection:vi.fn()
}));
vi.mock('./favoritesApi',()=>({
  loadFavorites:mocks.loadFavorites,startFavorite:mocks.startFavorite
}));
vi.mock('./wikiApi',async(importOriginal)=>{
  const real=await importOriginal<typeof import('./wikiApi')>();
  return {...real,readWikiSelection:mocks.readWikiSelection,
    saveWikiSelection:mocks.saveWikiSelection};
});

const wiki={
  id:'00000000-0000-4000-8000-000000000010',
  appId:'00000000-0000-4000-8000-000000000020',
  name:'Internes Wiki',appName:'Wiki',slug:'wiki',path:'/',
  username:'',autoLogin:false,enabled:true,sortOrder:0,hasPassword:false
};
function renderPage(){return render(<StrictMode><MemoryRouter><WikiPage/></MemoryRouter></StrictMode>);}

beforeEach(()=>{
  vi.clearAllMocks();
  mocks.loadFavorites.mockResolvedValue([wiki]);
  mocks.readWikiSelection.mockResolvedValue({favoriteId:wiki.id});
  mocks.saveWikiSelection.mockResolvedValue(undefined);
  mocks.startFavorite.mockResolvedValue({
    url:'https://apps-plenaro.example.org/wiki/_plenaro/favorite-bootstrap?token=test'
  });
});
afterEach(cleanup);

describe('Knowledge base / WikiBrowser migration',()=>{
  it('loads the selected personal wiki into the established proxy iframe',async()=>{
    renderPage();
    const frame=await screen.findByTitle('Wissensdatenbank – Internes Wiki');
    expect((frame as HTMLIFrameElement).src).toContain('/wiki/_plenaro/favorite-bootstrap?token=test');
    expect(frame.getAttribute('sandbox')).toContain('allow-same-origin');
    await waitFor(()=>expect(mocks.startFavorite).toHaveBeenCalledTimes(1));
    expect(screen.getByRole('heading',{name:'Wissensdatenbank'})).toBeTruthy();
  });

  it('does not embed an arbitrary or cross-favorite URL',async()=>{
    mocks.startFavorite.mockResolvedValue({url:
      'https://apps-plenaro.example.org/cloud/_plenaro/favorite-bootstrap?token=test'});
    renderPage();
    expect(await screen.findByText('Ungültige Wiki-Proxy-Adresse.')).toBeTruthy();
    expect(screen.queryByTitle('Wissensdatenbank – Internes Wiki')).toBeNull();
  });

  it('uses an existing wiki favorite when no default has been saved',async()=>{
    mocks.readWikiSelection.mockResolvedValue({favoriteId:null});
    renderPage();
    await screen.findByTitle('Wissensdatenbank – Internes Wiki');
    expect(mocks.startFavorite).toHaveBeenCalledWith(wiki.id);
  });

  it('never picks unrelated favorites without an explicit configuration',async()=>{
    mocks.readWikiSelection.mockResolvedValue({favoriteId:null});
    mocks.loadFavorites.mockResolvedValue([{...wiki,slug:'cloud',name:'Cloud'}]);
    renderPage();
    expect(await screen.findByText('Noch keine Wiki-Quelle ausgewählt')).toBeTruthy();
    expect(mocks.startFavorite).not.toHaveBeenCalled();
  });

  it('starts a fresh session only after the user requests reconnect',async()=>{
    renderPage();
    const frame=await screen.findByTitle('Wissensdatenbank – Internes Wiki');
    fireEvent.load(frame);
    const refresh=screen.getByRole('button',{name:/Neu verbinden/});
    await waitFor(()=>expect(refresh.hasAttribute('disabled')).toBe(false));
    fireEvent.click(refresh);
    await waitFor(()=>expect(mocks.startFavorite).toHaveBeenCalledTimes(2));
  });
});
