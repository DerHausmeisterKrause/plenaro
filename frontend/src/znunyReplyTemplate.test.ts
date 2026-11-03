import {describe,expect,it} from 'vitest';
import {customerName,ensureCloseNotice,renderReplyTemplate,customerArticle}
  from './znunyReplyTemplate';
import type {ZnunyArticle} from './znunySyncApi';
const entry=(from:string):ZnunyArticle=>({
  id:'1',subject:'Test',body:'Test',from,to:'support@example.org',
  replyTo:'',senderType:'customer',channel:'Email',
  created:'2026-11-02 12:00',contentType:'text/plain'
});
describe('WPF Znuny reply template',()=>{
  it('renders all six variables with actual customer source and date',()=>{
    const {text,unresolved}=renderReplyTemplate(
      'Hallo $Customer,\n$CustomerFirstName $CustomerLastName\n'+
      'Ticket $TicketNumber: $TicketTitle, Datum $Date',
      entry('"Max Mustermann" <max@example.org>'),'20260077','Monitor',
      new Date('2026-11-02T08:00:00Z'));
    expect(text).toContain('Hallo Max Mustermann,');
    expect(text).toContain('Max Mustermann\nTicket 20260077: Monitor, Datum 02.11.2026');
    expect(unresolved).toBe(false);
  });
  it('renders both escaped backslash-n and real newlines as multiline headers',()=>{
    const result=renderReplyTemplate('Hallo $Customer,\\n\\nTicket $TicketNumber',
      entry('Max <max@example.org>'),'2026#77','Test');
    expect(result.text).toBe('Hallo Max,\n\nTicket 2026#77');
  });
  it('keeps unknown placeholders for manual review rather than guessing names',()=>{
    const result=renderReplyTemplate('Hallo $Customer, $CustomerFirstName',
      entry('kunde@example.org'),'7','Hallo');
    expect(result.text).toContain('$Customer');
    expect(result.unresolved).toBe(true);
    expect(customerName('Kunde <kunde@example.org>')).toBe('Kunde');
  });
  it('adds the close notice once, directly after the greeting',()=>{
    expect(ensureCloseNotice('Hallo Kunde,\n\nVielen Dank.')).toBe(
      'Hallo Kunde,\n\nIst erfolgt. Ticket wird geschlossen.\n\nVielen Dank.');
    expect(ensureCloseNotice('Ist erfolgt. Ticket wird geschlossen.'))
      .toBe('Ist erfolgt. Ticket wird geschlossen.');
    expect(ensureCloseNotice('')).toBe('Ist erfolgt. Ticket wird geschlossen.');
  });
  it('recognizes customer sender without populated channel, as in legacy TicketGet',()=>{
    const x={...entry('Max <max@example.org>'),channel:''};
    expect(customerArticle([x])?.id).toBe(x.id);
  });
  it('recognizes inbound email-external even when SenderType is absent',()=>{
    const x={...entry('Max <max@example.org>'),senderType:'',
      channel:'email-external'};
    expect(customerArticle([x])?.id).toBe(x.id);
  });
  it('does not turn a pure agent article into a customer article',()=>{
    const x={...entry('Agent <agent@example.org>'),senderType:'agent',
      channel:'Email'};
    expect(customerArticle([x])).toBeUndefined();
    expect(renderReplyTemplate('Hallo $Customer',undefined,'77','Titel').unresolved)
      .toBe(true);
  });
  it('picks last customer email, not a recent agent reply',()=>{
    const articles=[entry('Anna <anna@example.org>'),{
      ...entry('Support <support@example.org>'),senderType:'agent'
    },{...entry('Lisa <lisa@example.org>'),id:'3'}];
    expect(customerArticle(articles)?.id).toBe('3');
  });
});
