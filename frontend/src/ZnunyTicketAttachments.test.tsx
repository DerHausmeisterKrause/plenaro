import {afterEach,expect,it,vi} from 'vitest';
import {cleanup,render,screen,waitFor} from '@testing-library/react';
import {ZnunyTicketConversationPanel} from './ZnunyTicketConversationPanel';
import {emailFrameDocument,normalizeCid,referencedCids,resolveCidImages} from './emailBody';
import {znunyAttachmentDownloadUrl} from './znunySyncApi';

afterEach(()=>{cleanup();vi.unstubAllGlobals();});

it('lists Znuny attachments as same-origin downloads and resolves only requested CID images',async()=>{
  const requests:string[]=[];
  vi.stubGlobal('fetch',vi.fn(async (url:string)=>{
    requests.push(String(url));
    if(String(url).endsWith('/inline'))return {
      ok:true,status:200,json:async()=>({dataUri:'data:image/png;base64,aGVsbG8='})
    };
    if(String(url).endsWith('/articles'))return {
      ok:true,status:200,json:async()=>({
        ticketId:'77',ticketNumber:'77',title:'Test',articles:[{
          id:'12',subject:'Logo',from:'Kunde',to:'helpdesk',replyTo:'',
          body:'<p>Text</p><img src="cid:logo@beispiel.de"><img src="https://tracking.invalid/t.png">',
          contentType:'text/html',senderType:'customer',channel:'Email',
          created:'2026-11-02 12:00',attachments:[
            {id:'5',filename:'logo.png',contentType:'image/png',contentId:'<LOGO@BEISPIEL.DE>',
              size:123,disposition:'inline'},
            {id:'6',filename:'Angebot.pdf',contentType:'application/pdf',contentId:'',
              size:10240,disposition:'attachment'},
          ]
        }]
      })
    };
    throw Error('Unexpected request: '+url);
  }));
  render(<ZnunyTicketConversationPanel ticketId="77" closed={true}/>);
  const link=await screen.findByRole('link',{name:/Angebot.pdf/});
  expect(link.getAttribute('href')).toBe('/api/v1/znuny/tickets/77/articles/12/attachments/6');
  expect(screen.getByText('Anhänge (2)')).toBeTruthy();
  const frame=await screen.findByTitle('HTML-E-Mail-Inhalt') as HTMLIFrameElement;
  await waitFor(()=>expect(frame.srcdoc).toContain('src="data:image/png;base64,aGVsbG8="'));
  expect(frame.getAttribute('sandbox')).toBe('');
  expect(frame.srcdoc).toContain('https://tracking.invalid/t.png');
  expect(frame.srcdoc).toContain("img-src data:");
  expect(requests.filter(url=>url.endsWith('/inline'))).toEqual([
    '/api/v1/znuny/tickets/77/articles/12/attachments/5/inline'
  ]);
  expect(requests).not.toContain(link.getAttribute('href'));
});

it('leaves unrecognized/missing CID references blank and rejects unsafe image URLs',()=>{
  expect(normalizeCid(' <IMG@Example.Org> ')).toBe('img@example.org');
  expect(referencedCids('<img src="cid:X@Host"><img src=\'cid:x@host\'>')).toEqual(['x@host']);
  const body='<img src="cid:safe"><img src="cid:unknown">';
  const rendered=resolveCidImages(body,{safe:'data:image/png;base64,YWJj'});
  expect(rendered).toContain('src="data:image/png;base64,YWJj"');
  expect(rendered).toContain('src="about:blank"');
  const malicious=resolveCidImages('<img src="cid:x">',{x:'javascript:alert(1)'});
  expect(malicious).not.toContain('javascript:');
  expect(emailFrameDocument(body)).not.toContain('src="cid:');
  expect(znunyAttachmentDownloadUrl('3','4','5'))
    .toBe('/api/v1/znuny/tickets/3/articles/4/attachments/5');
});
