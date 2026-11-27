import {StrictMode} from 'react';
import {afterEach,beforeEach,describe,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {MemoryRouter,Route,Routes} from 'react-router-dom';
import {FavoritePage,FavoriteSettingsPanel} from './FavoritesPage';
import {FavoriteDebugSettingsPanel} from './FavoriteDebugPanel';
import type {Favorite} from './favoritesApi';

const debugMocks=vi.hoisted(()=>({
  readFavoriteDebugSetting:vi.fn(),setFavoriteDebugSetting:vi.fn(),
  readFavoriteDebugEvents:vi.fn(),clearFavoriteDebugEvents:vi.fn(),
}));
vi.mock('./favoriteDebugApi',()=>debugMocks);

const mocks=vi.hoisted(()=>({
  loadFavorites:vi.fn(),
  startFavorite:vi.fn(),setFavoriteMode:vi.fn(),
  createPersonalFavorite:vi.fn(),updatePersonalFavorite:vi.fn(),
  removePersonalFavorite:vi.fn(),sortPersonalFavorites:vi.fn(),
}));
vi.mock('./favoritesApi',()=>({
  loadFavorites:mocks.loadFavorites,
  startFavorite:mocks.startFavorite,setFavoriteMode:mocks.setFavoriteMode,
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

function renderStrictFavorite(admin=false){
  return render(<StrictMode>
    <MemoryRouter initialEntries={['/favorites/'+favorite.id]}>
      <Routes><Route path="/favorites/:id" element={<FavoritePage admin={admin}/>} /></Routes>
    </MemoryRouter>
  </StrictMode>);
}

beforeEach(()=>{
  vi.clearAllMocks();
  debugMocks.readFavoriteDebugSetting.mockResolvedValue({enabled:true});
  debugMocks.setFavoriteDebugSetting.mockResolvedValue(undefined);
  debugMocks.readFavoriteDebugEvents.mockResolvedValue({enabled:true,events:[{
    id:1,at:'2026-11-24T08:00:00Z',level:'error',code:'upstream-unavailable',
    method:'GET',resource:'script',status:502
  }]});
  debugMocks.clearFavoriteDebugEvents.mockResolvedValue(undefined);
  mocks.loadFavorites.mockResolvedValue([favorite]);
  mocks.startFavorite.mockResolvedValue({
    url:'https://wiki.apps.example.org/_plenaro/favorite-bootstrap?token=example-test',
  });
});
afterEach(cleanup);

describe('Favorite iframe bootstrap',()=>{
  it('treats SharePoint Excel as a normal proxied favorite',async()=>{
    const url='https://tenant.sharepoint.com/:x:/r/sites/team/Shared%20Documents/plan.xlsx?web=1';
    mocks.loadFavorites.mockResolvedValue([{...favorite,name:'Excel',url}]);
    renderStrictFavorite();
    expect(await screen.findByTitle('Excel')).toBeTruthy();
    expect(mocks.startFavorite).toHaveBeenCalledTimes(1);
    expect(mocks.startFavorite).toHaveBeenCalledWith(favorite.id);
    expect(screen.getByRole('link',{name:'Originalseite öffnen'}).getAttribute('href')).toBe(url);
  });
  it('uses the direct sandboxed iframe without starting a proxy session',async()=>{
    mocks.loadFavorites.mockResolvedValue([{...favorite,openMode:'direct'}]);
    renderStrictFavorite();
    const frame=await screen.findByTitle('Wiki');
    expect(frame.getAttribute('src')).toBe(favorite.url);
    expect(frame.getAttribute('sandbox')).not.toContain('allow-same-origin');
    expect(mocks.startFavorite).not.toHaveBeenCalled();
  });
  it('opens a user-selected external favorite only through a browser link',async()=>{
    mocks.loadFavorites.mockResolvedValue([{...favorite,openMode:'external'}]);
    renderStrictFavorite();
    const link=await screen.findByRole('link',{name:'Im Browser öffnen'});
    expect(link.getAttribute('href')).toBe(favorite.url);
    expect(link.getAttribute('target')).toBe('_blank');
    expect(mocks.startFavorite).not.toHaveBeenCalled();
    expect(document.querySelector('iframe')).toBeNull();
  });
  it('allows switching the persisted mode even after a failed proxy start',async()=>{
    mocks.startFavorite.mockRejectedValueOnce(new Error('Nicht erreichbar'));
    mocks.setFavoriteMode.mockImplementation(async()=>{
      mocks.loadFavorites.mockResolvedValue([{...favorite,openMode:'external'}]);
    });
    renderStrictFavorite();
    await screen.findByText('Nicht erreichbar');
    fireEvent.change(screen.getByLabelText('Öffnungsmodus'),{target:{value:'external'}});
    expect(await screen.findByRole('link',{name:'Im Browser öffnen'})).toBeTruthy();
    expect(mocks.setFavoriteMode).toHaveBeenCalledWith(favorite.id,'external');
    expect(mocks.startFavorite).toHaveBeenCalledTimes(1);
  });
  it('shows network failure and allows reconnect',async()=>{
    mocks.startFavorite.mockRejectedValueOnce(new Error('Firewall-Freigabe und Routing prüfen.'));
    renderStrictFavorite();
    expect(await screen.findByText('Firewall-Freigabe und Routing prüfen.')).toBeTruthy();
    fireEvent.click(screen.getByRole('button',{name:'Neu verbinden'}));
    expect(await screen.findByTitle('Wiki')).toBeTruthy();
    expect(mocks.startFavorite).toHaveBeenCalledTimes(2);
  });

  it('hides diagnostic panels and makes no debug request for non-admin users',async()=>{
    renderStrictFavorite();
    expect(await screen.findByTitle('Wiki')).toBeTruthy();
    expect(screen.queryByRole('region',{name:'Favoriten-Diagnose'})).toBeNull();
    expect(debugMocks.readFavoriteDebugEvents).not.toHaveBeenCalled();
  });

  it('hides the entire debug card for admins unless enabled in settings',async()=>{
    debugMocks.readFavoriteDebugEvents.mockResolvedValue({
      enabled:false,events:[{
        id:1,at:'2026-11-24T08:00:00Z',level:'error',
        code:'upstream-unavailable',method:'GET',resource:'script',status:502
      }]
    });
    renderStrictFavorite(true);
    expect(await screen.findByTitle('Wiki')).toBeTruthy();
    await waitFor(()=>expect(debugMocks.readFavoriteDebugEvents)
      .toHaveBeenCalledWith(favorite.id,0));
    expect(screen.queryByRole('region',{name:'Favoriten-Diagnose'})).toBeNull();
    expect(screen.queryByText(/Debugmodus deaktiviert/)).toBeNull();
    expect(screen.queryByText(/Diagnoseeinstellungen werden geladen/)).toBeNull();
    expect(screen.queryByText(/Zielserver nicht erreichbar/)).toBeNull();
    expect(screen.getByRole('button',{name:'Neu verbinden'})).toBeTruthy();
  });

  it('uses a compact favorite header without the redundant multiline description',async()=>{
    renderStrictFavorite();
    expect(await screen.findByTitle('Wiki')).toBeTruthy();
    const toolbar=document.querySelector('.favorite-browser-toolbar');
    expect(toolbar).not.toBeNull();
    expect(toolbar?.querySelector('.favorite-browser-identity h1')?.textContent)
      .toBe('Wiki');
    expect(toolbar?.querySelector('.favorite-browser-actions')).not.toBeNull();
    expect(screen.queryByText('Plenaro / Favoriten')).toBeNull();
    expect(screen.queryByText('Geschützte Webanwendung mit eigener Browsersitzung.'))
      .toBeNull();
    expect(screen.getByRole('link',{name:'Originalseite öffnen'})).toBeTruthy();
    expect(screen.queryByRole('link',{name:'Favoriten verwalten'})).toBeNull();
  });

  it('shows safe proxy HTTP errors only for admin users',async()=>{
    renderStrictFavorite(true);
    expect(await screen.findByRole('region',{name:'Favoriten-Diagnose'})).toBeTruthy();
    expect(await screen.findByText(/Zielserver nicht erreichbar – Netzwerk und Firewall prüfen/)).toBeTruthy();
    expect(screen.getByText('502')).toBeTruthy();
    expect(debugMocks.readFavoriteDebugEvents).toHaveBeenCalledWith(favorite.id,0);
  });

  it('groups repeated upstream HTTP 400s by an opaque route tag',async()=>{
    debugMocks.readFavoriteDebugEvents.mockResolvedValue({enabled:true,events:[
      {id:7,at:'2026-11-24T12:20:30Z',level:'warning',
        code:'http-response',method:'POST',resource:'api',status:400,route:'A1B2C3D4E5'},
      {id:8,at:'2026-11-24T12:20:30Z',level:'warning',
        code:'upstream-400-csrf-hint',method:'POST',resource:'api',status:400,
        route:'A1B2C3D4E5'}
    ]});
    renderStrictFavorite(true);
    expect(await screen.findByText(/CSRF-\/XSRF-Prüfung könnte HTTP 400/)).toBeTruthy();
    expect(screen.getAllByText(/#A1B2C3D4E5/)).toHaveLength(2);
    fireEvent.click(screen.getByRole('checkbox',{name:'Nur Warnungen und Fehler anzeigen'}));
    expect(screen.getAllByText('400')).toHaveLength(2);
    expect(screen.queryByText(/top-secret|password=|token=/i)).toBeNull();
  });

  it('explains that a missing JS-only login needs a manual login on shared host',async()=>{
    debugMocks.readFavoriteDebugEvents.mockResolvedValue({enabled:true,events:[
      {id:9,at:'2026-11-24T12:21:30Z',level:'warning',
        code:'browser-worker-unreachable',method:'',resource:'other',status:null},
      {id:10,at:'2026-11-24T12:21:30Z',level:'warning',
        code:'html-login-form-not-found',method:'',resource:'other',status:null}
    ]});
    renderStrictFavorite(true);
    expect(await screen.findByText(/Browserdienst nicht erreichbar/)).toBeTruthy();
    expect(screen.getByText(/kein geeignetes klassisches HTML-Anmeldeformular/i)).toBeTruthy();
    expect(screen.queryByText(/Wildcard-DNS/)).toBeNull();
  });

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
  it('stores external mode from the explicit checkbox',async()=>{
    mocks.loadFavorites.mockResolvedValue(entries);
    mocks.updatePersonalFavorite.mockResolvedValue(undefined);
    mount();
    fireEvent.click(await screen.findByRole('button',{name:/Erste Webseite/}));
    fireEvent.click(screen.getByRole('checkbox',{name:'Favorit im externen Browser öffnen'}));
    fireEvent.click(screen.getByRole('button',{name:'Speichern'}));
    await waitFor(()=>expect(mocks.updatePersonalFavorite).toHaveBeenCalledWith(
      entries[0].id,expect.objectContaining({openMode:'external',sharedDomAutofill:false})));
    expect(screen.queryByRole('checkbox',{name:'Alternative Browser-Anmeldung (unsicher)'})).toBeNull();
  });
  it('requires explicit consent before enabling unsafe shared DOM autofill',async()=>{
    const confirm=vi.spyOn(window,'confirm').mockReturnValue(false);
    try{
      mocks.loadFavorites.mockResolvedValue([{...entries[0],username:'demo',autoLogin:true}]);
      mocks.updatePersonalFavorite.mockResolvedValue(undefined);
      mount();
      fireEvent.click(await screen.findByRole('button',{name:/Erste Webseite/}));
      const checkbox=screen.getByRole('checkbox',{name:'Alternative Browser-Anmeldung (unsicher)'}) as HTMLInputElement;
      expect(checkbox.checked).toBe(false);
      fireEvent.click(checkbox);expect(checkbox.checked).toBe(false);
      confirm.mockReturnValue(true);
      fireEvent.click(checkbox);expect(checkbox.checked).toBe(true);
      fireEvent.click(screen.getByRole('button',{name:'Speichern'}));
      await waitFor(()=>expect(mocks.updatePersonalFavorite).toHaveBeenCalledWith(
        entries[0].id,expect.objectContaining({sharedDomAutofill:true})));
    }finally{confirm.mockRestore();}
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


describe('Admin favoriten debug setting',()=>{
  it('persists an admin-only opt-in toggle from settings',async()=>{
    debugMocks.readFavoriteDebugSetting.mockResolvedValueOnce({enabled:false});
    render(<MemoryRouter><FavoriteDebugSettingsPanel/></MemoryRouter>);
    const box=await screen.findByRole('checkbox',{
      name:'Favoriten-Debugmodus für mein Administratorkonto aktivieren'
    });
    expect((box as HTMLInputElement).checked).toBe(false);
    fireEvent.click(box);
    await waitFor(()=>expect(debugMocks.setFavoriteDebugSetting).toHaveBeenCalledWith(true));
    await waitFor(()=>expect((box as HTMLInputElement).checked).toBe(true));
  });
});
