// Synthetic smoke test for the server-only Selenium Grid CDP websocket.
// No corporate hosts, credentials or external browser connectivity.
const session=process.env.SESSION;
if(!/^[a-zA-Z0-9_-]{10,160}$/.test(session??''))throw Error('missing CI session');
const socket=new WebSocket('ws://znuny-browser:4444/session/'+session+'/se/cdp');
let id=0,targetSession='',completed=false;
const timeout=setTimeout(()=>{console.error('CDP stream timed out');process.exit(1);},16000);
const send=(method,params={},sessionId)=>{
  const msg={id:++id,method,params};
  if(sessionId)msg.sessionId=sessionId;
  socket.send(JSON.stringify(msg));
};
socket.onopen=()=>send('Target.getTargets');
socket.onmessage=event=>{
  const msg=JSON.parse(event.data);
  if(msg.error){console.error('CDP error',msg.error.message);process.exit(1);}
  if(msg.id===1){
    const target=msg.result?.targetInfos?.find(t=>t.type==='page');
    if(!target)throw Error('CDP page target missing');
    send('Target.attachToTarget',{targetId:target.targetId,flatten:true});
  }else if(msg.id===2){
    targetSession=msg.result?.sessionId;
    if(!targetSession)throw Error('CDP attached session missing');
    send('Page.enable',{},targetSession);
  }else if(msg.id===3){
    send('Page.startScreencast',{
      format:'jpeg',quality:66,maxFramesInFlight:1
    },targetSession);
  }else if(msg.method==='Page.screencastFrame'){
    if(msg.sessionId!==targetSession)throw Error('wrong target session');
    const bytes=Buffer.from(msg.params.data,'base64');
    if(bytes[0]!==0xff||bytes[1]!==0xd8||bytes[2]!==0xff||bytes.length<100)
      throw Error('invalid compressed screenshot');
    completed=true;clearTimeout(timeout);
    console.log('CI checkpoint: CDP websocket screencast delivered JPEG frame');
    socket.close();process.exit(0);
  }
};
socket.onerror=event=>{console.error('CDP socket failed',event.message??'unknown');process.exit(1);};
socket.onclose=()=>{if(!completed){console.error('CDP socket closed before frame');process.exit(1);}};
