import {StrictMode} from 'react';
import {afterEach,beforeEach,describe,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {MemoryRouter,Route,Routes} from 'react-router-dom';
import {FavoritePage} from './FavoritesPage';

const mocks=vi.hoisted(()=>({
  loadFavorites:vi.fn(),
  startFavorite:vi.fn(),
}));
vi.mock('./favoritesApi',()=>({
  loadFavorites:mocks.loadFavorites,
  startFavorite:mocks.startFavorite,
}));

const favorite={
  id:'00000000-0000-4000-8000-000000000010',
  appId:'00000000-0000-4000-8000-000000000020',
  name:'Wiki',appName:'Wiki',slug:'wiki',path:'/',
  username:'',autoLogin:false,enabled:true,sortOrder:0,hasPassword:false,
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
