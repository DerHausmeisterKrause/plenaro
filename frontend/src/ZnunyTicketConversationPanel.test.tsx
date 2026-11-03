import {afterEach,describe,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen} from '@testing-library/react';
import {ZnunyTicketConversationPanel} from './ZnunyTicketConversationPanel';
import {emailFrameDocument,isHtmlMail} from './emailBody';

afterEach(()=>{cleanup();vi.unstubAllGlobals();});

function mockArticle(body:string,contentType:string){
  vi.stubGlobal('fetch',vi.fn(async()=>({
    ok:true,status:200,json:async()=>({
      ticketId:'77',ticketNumber:'2026#77',title:'E-Mail-Test',
      articles:[{id:'1',subject:'Test & Sonderzeichen',body,contentType,
        from:'Müller <mueller@example.invalid>',to:'helpdesk@example.invalid',replyTo:'',
        senderType:'customer',channel:'Email',created:'2026-11-02 12:00:00'}],
    }),
  })));
}

describe('Znuny Ticket E-Mail Anzeige',()=>{
  it('preserves text mail with line breaks and angle brackets as escaped React text',async()=>{
    mockArticle('Guten Tag,\n\nZeile 2 <script>alert(1)</script>\nGrüße','text/plain; charset=utf-8');
    render(<ZnunyTicketConversationPanel ticketId="77" closed={true}/>);
    const content=await screen.findByLabelText('Nachrichtentext');
    expect(content.textContent).toContain('Guten Tag,\n\nZeile 2 <script>alert(1)</script>');
    expect(content.querySelector('script')).toBeNull();
    expect(screen.queryByTitle('HTML-E-Mail-Inhalt')).toBeNull();
  });
  it('renders real HTML mail in a sandbox frame without remote images or JS',async()=>{
    mockArticle('<p>Hallo <strong>Welt</strong></p><table><tr><td>Format</td></tr></table><img src="https://tracker.invalid/open">',
      'text/html; charset=UTF-8');
    render(<ZnunyTicketConversationPanel ticketId="77" closed={true}/>);
    const frame=await screen.findByTitle('HTML-E-Mail-Inhalt') as HTMLIFrameElement;
    expect(frame.getAttribute('sandbox')).toBe('');
    expect(frame.getAttribute('referrerpolicy')).toBe('no-referrer');
    expect(frame.srcdoc).toContain('<strong>Welt</strong>');
    expect(frame.srcdoc).toContain('<table>');
    expect(frame.srcdoc).toContain("default-src 'none'");
    expect(frame.srcdoc).toContain('img-src data:');
    expect(frame.srcdoc).toContain("script-src 'none'");
    expect(screen.getByText(/HTML-E-Mail/)).toBeTruthy();
    fireEvent.click(screen.getByRole('button',{name:'Ansicht vergrößern'}));
    expect(frame.className).toContain('znuny-email-frame-expanded');
  });
  it('recognizes HTML markup even if Znuny omits MIME metadata',()=>{
    expect(isHtmlMail('<div>Nachricht<br>Text</div>')).toBe(true);
    expect(isHtmlMail('Nur <beispiel> in Text')).toBe(false);
    expect(isHtmlMail('<p>Test</p>','text/plain')).toBe(false);
    const document=emailFrameDocument('<p>Test</p><meta http-equiv="refresh" content="0;url=https://tracker.invalid">');
    expect(document).toContain('<p>Test</p>');
    expect(document).not.toContain('http-equiv="refresh"');
    expect(document).toContain('form-action \'none\'');
  });
});
