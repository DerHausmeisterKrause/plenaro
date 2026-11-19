import {StrictMode} from 'react';
import {afterEach,beforeEach,describe,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {MemoryRouter,Route,Routes} from 'react-router-dom';
import {FavoritePage,FavoriteSettingsPanel} from './FavoritesPage';
import type {Favorite} from './favoritesApi';

const mocks=vi.hoisted(()=>({
  loadFavorites:vi.fn(),
  startFavorite:vi.fn(),
  createPersonalFavorite:vi.fn(),updatePersonalFavorite:vi.fn(),
  removePersonalFavorite:vi.fn(),sortPersonalFavorites:vi.fn(),
}));
vi.mock('./favoritesApi',()=>({
  loadFavorites:mocks.loadFavorites,
  startFavorite:mocks.startFavorite,
  createPersonalFavorite:mocks.createPersonalFavorite,
  updatePersonalFavorite:mocks.updatePersonalFavorite,
  removePersonalFavorite:mocks.removePersonalFavorite,
  sortPersonalFavorites:mocks.sortPersonalFavorites,
}));

const favorite={
  id:'00000000-0000-4000-8000-000000000010',
  appId:'00000000-0000-4000-8000-000000000020',
  name:'Wiki',appName:'Wiki',slug:'wiki',path:'/',
  username:'',autoLogin:false,enabled:true,sortOrder:0,hasPassword:false,
  url:'https://example.org/docs',
};

function renderStrictFavorite(){
  return render(<StrictMode>
    <MemoryRouter initialEntries={['/favorites/'+favorite.id]}>
      <Routes><Route path="/favorites/:id" element={<FavoritePage/>}/></Routes>
    </MemoryRouter>
  </StrictMode>);
}

beforeEach(()=>{
  vi.clearAllMocks();
  mocks.loadFavorites.mockResolvedValue([favorite]);
  mocks.startFavorite.mockResolvedValue({
    url:'https://wiki.apps.example.org/_plenaro/favorite-bootstrap?token=example-test',
  });
});
afterEach(cleanup);

describe('Favorite iframe bootstrap',()=>{
  it('starts one proxy session despite StrictMode double effect',async()=>{
    renderStrictFavorite();
    const frame=await screen.findByTitle('Wiki');
    expect((frame as HTMLIFrameElement).src).toContain(
      '/_plenaro/favorite-bootstrap?token=example-test');
    await waitFor(()=>expect(mocks.startFavorite).toHaveBeenCalledTimes(1));
    expect(mocks.loadFavorites).toHaveBeenCalledTimes(1);
    const original=screen.getByRole('link',{name:'Originalseite öffnen'});
    expect(original.getAttribute('href')).toBe('https://example.org/docs');
    expect(original.getAttribute('target')).toBe('_blank');
    expect(original.getAttribute('rel')).toContain('noopener');
  });

  it('accepts the shared path-host bootstrap for the selected favorite',async()=>{
    mocks.startFavorite.mockResolvedValue({
      url:'https://apps-plenaro.example.org/wiki/_plenaro/favorite-bootstrap?token=example-test',
    });
    renderStrictFavorite();
    const frame=await screen.findByTitle('Wiki');
    expect((frame as HTMLIFrameElement).src).toContain(
      '/wiki/_plenaro/favorite-bootstrap?token=example-test');
    expect(screen.queryByRole('alert')).toBeNull();
    await waitFor(()=>expect(mocks.startFavorite).toHaveBeenCalledTimes(1));
  });

  it('does not interrupt a working shared favorite with a persistent autofill warning',async()=>{
    const site={...favorite,name:'ATOSS',slug:'site-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
      autoLogin:true,hasPassword:true};
    mocks.loadFavorites.mockResolvedValue([site]);
    mocks.startFavorite.mockResolvedValue({
      url:'https://apps-plenaro.example.org/site-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/'+
        '_plenaro/favorite-bootstrap?token=example-test',
    });
    renderStrictFavorite();
    expect(await screen.findByTitle('ATOSS')).toBeTruthy();
    expect(screen.queryByText('Automatisches Ausfüllen noch nicht aktiv')).toBeNull();
    expect(screen.queryByText(/Wildcard-DNS/)).toBeNull();
  });

  it('keeps the warning hidden when a personal favorite has its own origin',async()=>{
    const site={...favorite,name:'ATOSS',slug:'site-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
      autoLogin:true,hasPassword:true};
    mocks.loadFavorites.mockResolvedValue([site]);
    mocks.startFavorite.mockResolvedValue({
      url:'https://site-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-plenaro.example.org/'+
        '_plenaro/favorite-bootstrap?token=example-test',
    });
    renderStrictFavorite();
    expect(await screen.findByTitle('ATOSS')).toBeTruthy();
    expect(screen.queryByText('Automatisches Ausfüllen noch nicht aktiv')).toBeNull();
  });

  it('rejects a bootstrap path for another favorite on the shared host',async()=>{
    mocks.startFavorite.mockResolvedValue({
      url:'https://apps-plenaro.example.org/cloud/_plenaro/favorite-bootstrap?token=example-test',
    });
    renderStrictFavorite();
    expect(await screen.findByText('Ungültige Favoriten-Proxy-Adresse.')).toBeTruthy();
    expect(screen.queryByTitle('Wiki')).toBeNull();
  });

  it('starts a new session only on an explicit reconnect',async()=>{
    renderStrictFavorite();
    const frame=await screen.findByTitle('Wiki');
    fireEvent.load(frame);
    const reconnect=screen.getByRole('button',{name:/Neu verbinden/});
    await waitFor(()=>expect(reconnect).not.toHaveProperty('disabled',true));
    fireEvent.click(reconnect);
    await waitFor(()=>expect(mocks.startFavorite).toHaveBeenCalledTimes(2));
  });
});


describe('Personal self-service favorite management',()=>{
  const entries:Favorite[]=[
    {id:'10000000-0000-4000-8000-000000000001',
     appId:'10000000-0000-4000-8000-000000000011',
     name:'Erste Webseite',appName:'Erste Webseite',slug:'site-a',
     url:'https://example.org/notes',path:'/notes',username:'',
     autoLogin:false,enabled:true,sortOrder:0,hasPassword:false},
    {id:'10000000-0000-4000-8000-000000000002',
     appId:'10000000-0000-4000-8000-000000000012',
     name:'Zweite Webseite',appName:'Zweite Webseite',slug:'site-b',
     url:'https://example.com/search',path:'/search',username:'',
     autoLogin:false,enabled:true,sortOrder:1,hasPassword:false}
  ];
  function mount(){return render(<MemoryRouter>
    <FavoriteSettingsPanel onChanged={vi.fn()}/>
  </MemoryRouter>);}
  it('creates a personal HTTPS URL without requiring a catalog or admin approval',async()=>{
    mocks.loadFavorites.mockResolvedValue([]);
    mocks.createPersonalFavorite.mockResolvedValue({id:entries[0].id});
    mount();
    expect(await screen.findByText('Noch keine Webseiten vorhanden.')).toBeTruthy();
    expect(screen.queryByLabelText('Freigegebene Webseite')).toBeNull();
    fireEvent.change(screen.getByLabelText('Name der Webseite'),{
      target:{value:'Erste Webseite'}
    });
    fireEvent.change(screen.getByLabelText('Webadresse (HTTPS)'),{
      target:{value:'https://example.org/notes'}
    });
    fireEvent.click(screen.getByRole('button',{name:'Speichern'}));
    await waitFor(()=>expect(mocks.createPersonalFavorite).toHaveBeenCalledWith(
      expect.objectContaining({
        name:'Erste Webseite',url:'https://example.org/notes',
        autoLogin:false,enabled:true,sortOrder:0
      })
    ));
  });
  it('persists independent sorting and can edit an existing URL',async()=>{
    mocks.loadFavorites.mockResolvedValue(entries);
    mocks.sortPersonalFavorites.mockResolvedValue(undefined);
    mocks.updatePersonalFavorite.mockResolvedValue(undefined);
    mount();
    fireEvent.click(await screen.findByRole('button',{name:/Zweite Webseite/}));
    fireEvent.click(screen.getByRole('button',{name:'Favorit nach oben'}));
    await waitFor(()=>expect(mocks.sortPersonalFavorites).toHaveBeenCalledWith(
      [entries[1].id,entries[0].id]
    ));
    fireEvent.change(screen.getByLabelText('Webadresse (HTTPS)'),{
      target:{value:'https://example.net/updated'}
    });
    fireEvent.click(screen.getByRole('button',{name:'Speichern'}));
    await waitFor(()=>expect(mocks.updatePersonalFavorite).toHaveBeenCalledWith(
      entries[1].id,expect.objectContaining({url:'https://example.net/updated'})
    ));
  });
});
