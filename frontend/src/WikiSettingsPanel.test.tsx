import {afterEach,beforeEach,describe,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {MemoryRouter} from 'react-router-dom';
import {WikiSettingsPanel} from './WikiSettingsPanel';

const mocks=vi.hoisted(()=>({
  readWikiSettings:vi.fn(),readWikiAdmin:vi.fn(),
  saveWikiLogin:vi.fn(),deleteWikiLogin:vi.fn(),saveWikiAdmin:vi.fn()
}));
vi.mock('./wikiApi',()=>({
  readWikiSettings:mocks.readWikiSettings,readWikiAdmin:mocks.readWikiAdmin,
  saveWikiLogin:mocks.saveWikiLogin,deleteWikiLogin:mocks.deleteWikiLogin,
  saveWikiAdmin:mocks.saveWikiAdmin
}));
const personal={
  configured:true,name:'Firmenwiki',homePath:'/',username:'alice',
  hasPassword:true,ready:true,
  favoriteId:'00000000-0000-4000-8000-000000000010'
};
const adminConfig={
  configured:true,name:'Firmenwiki',baseUrl:'https://wiki.example.org/',homePath:'/'
};
function renderPanel(admin=false){return render(<MemoryRouter>
  <WikiSettingsPanel admin={admin}/>
</MemoryRouter>);}
beforeEach(()=>{
  vi.clearAllMocks();
  mocks.readWikiSettings.mockResolvedValue(personal);
  mocks.readWikiAdmin.mockResolvedValue(adminConfig);
  mocks.saveWikiLogin.mockResolvedValue(undefined);
  mocks.saveWikiAdmin.mockResolvedValue(undefined);
  mocks.deleteWikiLogin.mockResolvedValue(undefined);
});
afterEach(cleanup);

describe('Wiki settings – central admin, private user login',()=>{
  it('only shows personal credentials to a normal user',async()=>{
    renderPanel();
    expect(await screen.findByLabelText('Mein Wiki-Benutzername')).toBeTruthy();
    expect(screen.queryByLabelText('Wiki-Serveradresse (HTTPS)')).toBeNull();
    expect(mocks.readWikiAdmin).not.toHaveBeenCalled();
  });
  it('saves each user login without resetting an existing encrypted password',async()=>{
    renderPanel();
    const name=await screen.findByLabelText('Mein Wiki-Benutzername');
    fireEvent.change(name,{target:{value:'bob'}});
    fireEvent.click(screen.getByRole('button',{name:'Wiki-Zugangsdaten speichern'}));
    await waitFor(()=>expect(mocks.saveWikiLogin).toHaveBeenCalledWith('bob',''));
  });
  it('requires a password if none was previously configured',async()=>{
    mocks.readWikiSettings.mockResolvedValue({
      ...personal,username:'',hasPassword:false,ready:false,favoriteId:null
    });
    renderPanel();
    const password=await screen.findByLabelText('Mein Wiki-Passwort');
    expect(password.hasAttribute('required')).toBe(true);
  });
  it('allows an admin to save exactly one server configuration for all users',async()=>{
    renderPanel(true);
    const uri=await screen.findByLabelText('Wiki-Serveradresse (HTTPS)');
    fireEvent.change(uri,{target:{value:'https://wiki.new.example.org/'}});
    fireEvent.change(screen.getByLabelText('Wiki-Startseite / Pfad'),{
      target:{value:'/display/IT'}
    });
    fireEvent.click(screen.getByRole('button',{
      name:'Zentrale Wiki-Konfiguration speichern'
    }));
    await waitFor(()=>expect(mocks.saveWikiAdmin).toHaveBeenCalledWith({
      name:'Firmenwiki',baseUrl:'https://wiki.new.example.org/',homePath:'/display/IT'
    }));
  });
  it('never exposes the admin server UI when Wiki is unconfigured',async()=>{
    mocks.readWikiSettings.mockResolvedValue({...personal,configured:false,ready:false});
    renderPanel();
    expect(await screen.findByText(/Der Administrator hat noch keinen zentralen Wiki-Server/)).toBeTruthy();
    expect(screen.queryByLabelText('Wiki-Serveradresse (HTTPS)')).toBeNull();
  });
});
