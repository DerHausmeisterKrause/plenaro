import {afterEach,beforeEach,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {MemoryRouter,Link} from 'react-router-dom';
import {RetainedWebWorkspace} from './RetainedWebWorkspace';
const mocks=vi.hoisted(()=>({currentUser:vi.fn()}));
vi.mock('./authApi',()=>mocks);
vi.mock('./FavoritesPage',()=>({FavoritePage:({favoriteId}:{favoriteId:string})=>
  <iframe title={'favorite '+favoriteId} src={'https://favorites.example.org/'+favoriteId}/>}));
vi.mock('./WikiPage',()=>({WikiPage:()=> <iframe title="wiki" src="https://wiki.example.org/"/>}));
vi.mock('./ZnunyWebPortalPage',()=>({ZnunyWebPortalPage:({retainedTicket,navigationKey}:{
  retainedTicket:string|null;navigationKey?:string})=>
  <iframe title="ticket" key={navigationKey} src={'https://tickets.example.org/'+(retainedTicket??'home')}/>}));
const onInvalid=vi.fn();
function setup(path='/favorites/a'){
  return render(<MemoryRouter initialEntries={[path]}>
    <nav>
      <Link to="/favorites/a">A</Link><Link to="/favorites/b">B</Link>
      <Link to="/wiki">Wiki</Link><Link to="/ticket-system">Tickets</Link>
      <Link to="/ticket-system?ticket=77">Ticket 77</Link>
      <Link to="/ticket-system?ticket=78">Ticket 78</Link>
      <Link to="/workspace">Workspace</Link><Link to="/settings">Settings</Link>
      {Array.from({length:9},(_,i)=><Link key={i} to={'/favorites/f'+i}>F{i}</Link>)}
    </nav>
    <RetainedWebWorkspace userId="user-a" admin={false} onInvalidSession={onInvalid}/>
  </MemoryRouter>);
}
const click=(name:string)=>fireEvent.click(screen.getByRole('link',{name}));
const visible=(frame:HTMLElement)=>!frame.closest('.retained-web-page')?.hasAttribute('hidden');
beforeEach(()=>{vi.clearAllMocks();mocks.currentUser.mockResolvedValue({id:'user-a',roles:[]});});
afterEach(cleanup);
it('retains exactly the same DOM iframe when switching favorites, wiki, tickets and ordinary routes',async()=>{
  setup();
  const a=await screen.findByTitle('favorite a');
  click('B');const b=await screen.findByTitle('favorite b');
  expect(visible(a)).toBe(false);
  click('Wiki');const wiki=await screen.findByTitle('wiki');
  click('Tickets');const ticket=await screen.findByTitle('ticket');
  click('Workspace');expect(visible(ticket)).toBe(false);
  click('A');await waitFor(()=>expect(visible(a)).toBe(true));
  expect(screen.getByTitle('favorite a')).toBe(a);
  expect(screen.getByTitle('favorite b')).toBe(b);
  expect(screen.getByTitle('wiki')).toBe(wiki);
  click('Tickets');await waitFor(()=>expect(visible(ticket)).toBe(true));
  expect(screen.getByTitle('ticket')).toBe(ticket);
  click('Wiki');await waitFor(()=>expect(visible(wiki)).toBe(true));
  expect(screen.getByTitle('wiki')).toBe(wiki);
});
it('opens explicitly requested tickets but sidebar resumes the last native page',async()=>{
  setup('/ticket-system?ticket=77');
  const first=await screen.findByTitle('ticket');
  expect(first.getAttribute('src')).toContain('/77');
  click('A');await screen.findByTitle('favorite a');
  click('Tickets');await waitFor(()=>expect(visible(first)).toBe(true));
  expect(screen.getByTitle('ticket')).toBe(first);
  click('Ticket 78');
  await waitFor(()=>expect(screen.getByTitle('ticket').getAttribute('src')).toContain('/78'));
  expect(screen.getByTitle('ticket')).not.toBe(first);
});
it('invalidates retained pages when settings are opened or account changes',async()=>{
  setup();
  const first=await screen.findByTitle('favorite a');
  click('Settings');expect(screen.queryByTitle('favorite a')).toBeNull();
  click('A');const fresh=await screen.findByTitle('favorite a');
  expect(fresh).not.toBe(first);
  click('Workspace');mocks.currentUser.mockResolvedValue({id:'user-b'});
  click('A');await waitFor(()=>expect(onInvalid).toHaveBeenCalled());
  expect(screen.queryByTitle('favorite a')).toBeNull();
});
it('bounds favorite frames to eight without evicting the wiki or native ticket frame',async()=>{
  setup('/wiki');const wiki=await screen.findByTitle('wiki');
  click('Tickets');const ticket=await screen.findByTitle('ticket');
  for(let i=0;i<9;i++){click('F'+i);await screen.findByTitle('favorite f'+i);}
  expect(screen.queryByTitle('favorite f0')).toBeNull();
  expect(document.querySelectorAll('iframe')).toHaveLength(10);
  expect(screen.getByTitle('wiki')).toBe(wiki);
  expect(screen.getByTitle('ticket')).toBe(ticket);
});
