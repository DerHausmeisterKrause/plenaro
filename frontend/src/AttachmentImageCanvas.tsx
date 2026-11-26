import {useEffect,useRef,useState} from 'react';

// The file is already scanned by the server. Decode the actual bytes instead
// of assigning a blob URL to img.src (which can be blocked by a proxy CSP).
// Never use the original upstream URL or inject any attachment HTML.
export function AttachmentImageCanvas({file,name}:{file:Blob;name:string}){
  const canvas=useRef<HTMLCanvasElement>(null);
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState('');
  useEffect(()=>{
    let active=true;
    const element=canvas.current;
    setLoading(true);setError('');
    if(!element)return;
    void(async()=>{
      let bitmap:ImageBitmap|undefined;
      try{
        if(typeof createImageBitmap!=='function')
          throw new Error('Dieser Browser unterstützt die Bilddekodierung für die Vorschau nicht.');
        bitmap=await createImageBitmap(file);
        if(!active)return;
        if(!bitmap.width||!bitmap.height)
          throw new Error('Das Bild hat keine gültigen Abmessungen.');
        // Bound canvas allocation; retain aspect ratio for large images.
        const scale=Math.min(1,4096/bitmap.width,4096/bitmap.height,
          Math.sqrt(8_000_000/(bitmap.width*bitmap.height)));
        element.width=Math.max(1,Math.floor(bitmap.width*scale));
        element.height=Math.max(1,Math.floor(bitmap.height*scale));
        const context=element.getContext('2d');
        if(!context)throw new Error('Die Bilddarstellung ist im Browser nicht verfügbar.');
        context.drawImage(bitmap,0,0,element.width,element.height);
        setLoading(false);
      }catch(e){
        if(!active)return;
        setLoading(false);
        setError(e instanceof Error&&e.message.startsWith('Dieser Browser')?e.message:
          'Der Browser konnte die geprüften Bilddaten nicht dekodieren oder darstellen ('+
          file.type+', '+file.size.toLocaleString('de-DE')+' Bytes).');
      }finally{bitmap?.close();}
    })();
    return()=>{active=false;element.width=0;element.height=0;};
  },[file]);
  return <div className="attachment-preview-image-wrap">
    {loading&&<p role="status">Bild wird dargestellt …</p>}
    {error&&<p role="alert" className="um-feedback failure">{error}</p>}
    <canvas ref={canvas} role="img" aria-label={name||'Anhang'}
      className="attachment-preview-image" hidden={loading||!!error}/>
  </div>;
}
