import {afterEach,beforeEach,describe,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {MemoryRouter} from 'react-router-dom';
import {WikiSettingsPanel} from './WikiSettingsPanel';

const mocks=vi.hoisted(()=>({
  loadFavorites:vi.fn(),loadFavoriteApps:vi.fn(),loadApprovedApps:vi.fn(),
  createFavorite:vi.fn(),createApprovedApp:vi.fn(),
  readWikiSelection:vi.fn(),saveWikiSelection:vi.fn()
}));
vi.mock('./favoritesApi',()=>({
  loadFavorites:mocks.loadFavorites,loadFavoriteApps:mocks.loadFavoriteApps,
  loadApprovedApps:mocks.loadApprovedApps,createFavorite:mocks.createFavorite,
  createApprovedApp:mocks.createApprovedApp
}));
vi.mock('./wikiApi',()=>({
  readWikiSelection:mocks.readWikiSelection,saveWikiSelection:mocks.saveWikiSelection
}));

const appId='00000000-0000-4000-8000-000000000020';
const wikiId='00000000-0000-4000-8000-000000000010';
const wiki={id:wikiId,appId,name:'Internes Wiki',appName:'Wiki',slug:'wiki',
  path:'/',username:'',autoLogin:false,enabled:true,sortOrder:0,hasPassword:false};

function renderPanel(admin=false){
  return render(<MemoryRouter>
    <WikiSettingsPanel admin={admin} userId="00000000-0000-4000-8000-000000000001"/>
  </MemoryRouter>);
}

beforeEach(()=>{
  vi.clearAllMocks();
  mocks.loadFavorites.mockResolvedValue([wiki]);
  mocks.loadFavoriteApps.mockResolvedValue([{id:appId,name:'Wiki',slug:'wiki'}]);
  mocks.loadApprovedApps.mockResolvedValue([]);
  mocks.readWikiSelection.mockResolvedValue({favoriteId:wikiId});
  mocks.saveWikiSelection.mockResolvedValue(undefined);
  mocks.createFavorite.mockResolvedValue({id:wikiId});
  mocks.createApprovedApp.mockResolvedValue({id:appId});
});
afterEach(cleanup);

describe('Wiki source settings',()=>{
  it('selects and persists a pre-existing, working personal Wiki favorite',async()=>{
    renderPanel();
    const select=await screen.findByLabelText('Standard-Wiki') as HTMLSelectElement;
    expect(select.value).toBe(wikiId);
    fireEvent.click(screen.getByRole('button',{name:'Standard speichern'}));
    await waitFor(()=>expect(mocks.saveWikiSelection).toHaveBeenCalledWith(wikiId));
    expect(mocks.createFavorite).not.toHaveBeenCalled();
  });

  it('creates a personal wiki source from an admin-approved application',async()=>{
    mocks.loadFavorites.mockResolvedValue([]);
    mocks.readWikiSelection.mockResolvedValue({favoriteId:null});
    renderPanel();
    const app=await screen.findByLabelText('Freigegebener Wiki-Server') as HTMLSelectElement;
    await waitFor(()=>expect(app.value).toBe(appId));
    fireEvent.change(screen.getByLabelText('Name der Wiki-Quelle'),{
      target:{value:'Betriebsdokumentation'}});
    fireEvent.change(screen.getByLabelText('Startseite / Pfad'),{
      target:{value:'/display/TEAM'}});
    fireEvent.click(screen.getByRole('button',{name:/Wiki hinzufügen und als Standard/}));
    await waitFor(()=>expect(mocks.createFavorite).toHaveBeenCalledWith(
      expect.objectContaining({appId,name:'Betriebsdokumentation',path:'/display/TEAM'})));
    await waitFor(()=>expect(mocks.saveWikiSelection).toHaveBeenCalledWith(wikiId));
  });

  it('does not expose upstream server registration to non-admins',async()=>{
    renderPanel();
    await screen.findByLabelText('Standard-Wiki');
    expect(screen.queryByLabelText('Wiki-Adresse (HTTPS)')).toBeNull();
    expect(mocks.loadApprovedApps).not.toHaveBeenCalled();
  });

  it('allows admins to approve HTTPS origin and preserves a page start path',async()=>{
    mocks.loadFavorites.mockResolvedValue([]);
    mocks.readWikiSelection.mockResolvedValue({favoriteId:null});
    renderPanel(true);
    const input=await screen.findByLabelText('Wiki-Adresse (HTTPS)');
    fireEvent.change(input,{target:{value:'https://wiki.example.org/display/TEAM'}});
    fireEvent.click(screen.getByRole('button',{name:'Wiki-Server freigeben'}));
    await waitFor(()=>expect(mocks.createApprovedApp).toHaveBeenCalledWith(
      expect.objectContaining({slug:'wiki',upstreamUrl:'https://wiki.example.org/',
        availableToAll:false})));
  });

  it('rejects direct insecure HTTP URLs even for administrators',async()=>{
    renderPanel(true);
    const input=await screen.findByLabelText('Wiki-Adresse (HTTPS)');
    fireEvent.change(input,{target:{value:'http://wiki.example.org/'}});
    fireEvent.click(screen.getByRole('button',{name:'Wiki-Server freigeben'}));
    expect(mocks.createApprovedApp).not.toHaveBeenCalled();
  });
});
