/// <reference types="vite/client" />
import {useEffect,useRef,useState} from 'react';
import {ChevronLeft,ChevronRight,Minus,Plus} from 'lucide-react';
import type {PDFDocumentProxy,PDFDocumentLoadingTask,RenderTask} from 'pdfjs-dist';
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';

export default function AttachmentPdfPage({file}:{file:Blob}){
  const [document,setDocument]=useState<PDFDocumentProxy|null>(null);
  const [page,setPage]=useState(1);
  const [zoom,setZoom]=useState(1);
  const [error,setError]=useState('');
  const [rendering,setRendering]=useState(true);
  const canvas=useRef<HTMLCanvasElement>(null);
  useEffect(()=>{
    let active=true;
    let task:PDFDocumentLoadingTask|undefined;
    setRendering(true);setError('');setDocument(null);setPage(1);
    void(async()=>{
      try{
        const pdfjs=await import('pdfjs-dist');
        const data=new Uint8Array(await file.arrayBuffer());
        if(!active)return;
        pdfjs.GlobalWorkerOptions.workerSrc=workerUrl;
        task=pdfjs.getDocument({
          data,enableXfa:false,useWasm:false,isOffscreenCanvasSupported:false,
          isImageDecoderSupported:false,maxImageSize:16_000_000,
          cMapUrl:'/pdfjs/cmaps/',cMapPacked:true,
          standardFontDataUrl:'/pdfjs/standard_fonts/',
        });
        // Do not request or run any JavaScript actions, link annotations,
        // attachments, or interactive forms. Only canvas pixels are shown.
        const loaded=await task.promise;
        if(!active)return;
        if(loaded.numPages<1||loaded.numPages>500)
          throw new Error('Das PDF überschreitet das Seitenlimit der Vorschau.');
        setDocument(loaded);
      }catch(e){
        if(active){setRendering(false);setError(e instanceof Error&&e.message.includes('Seitenlimit')
          ?e.message:'PDF kann nicht dargestellt werden (z. B. beschädigt oder passwortgeschützt).');}
      }
    })();
    return()=>{active=false;if(task)void task.destroy().catch(()=>{});};
  },[file]);
  useEffect(()=>{
    if(!document)return;
    let active=true;
    let render:RenderTask|undefined;
    setRendering(true);setError('');
    void(async()=>{
      try{
        const selected=await document.getPage(page);
        const element=canvas.current;
        if(!active||!element)return;
        const base=selected.getViewport({scale:1});
        if(!Number.isFinite(base.width)||!Number.isFinite(base.height)||base.width<=0||base.height<=0)
          throw new Error('Invalid page size');
        // Bound canvas memory, including unusual PDF page dimensions.
        const scale=Math.min(1.5*zoom,2400/base.width,3200/base.height,
          Math.sqrt(6_000_000/(base.width*base.height)));
        const viewport=selected.getViewport({scale});
        element.width=Math.max(1,Math.floor(viewport.width));
        element.height=Math.max(1,Math.floor(viewport.height));
        const context=element.getContext('2d',{alpha:false});
        if(!context)throw new Error('Canvas unavailable');
        render=selected.render({canvas:element,canvasContext:context,viewport,
          annotationMode:0,background:'#ffffff'});
        await render.promise;
        if(active)setRendering(false);
      }catch{
        if(active){setRendering(false);setError('PDF-Seite konnte nicht dargestellt werden.');}
      }
    })();
    return()=>{active=false;render?.cancel();};
  },[document,page,zoom]);
  return <div className="attachment-pdf">
    {document&&<div className="attachment-pdf-toolbar">
      <button type="button" className="button button-secondary" aria-label="Vorherige PDF-Seite"
        disabled={page<=1||rendering} onClick={()=>setPage(p=>p-1)}><ChevronLeft size={17}/></button>
      <span>Seite {page} / {document.numPages}</span>
      <button type="button" className="button button-secondary" aria-label="Nächste PDF-Seite"
        disabled={page>=document.numPages||rendering} onClick={()=>setPage(p=>p+1)}><ChevronRight size={17}/></button>
      <button type="button" className="button button-secondary" aria-label="Verkleinern"
        disabled={zoom<=.5||rendering} onClick={()=>setZoom(z=>Math.max(.5,z-.25))}><Minus size={16}/></button>
      <button type="button" className="button button-secondary" aria-label="Vergrößern"
        disabled={zoom>=2||rendering} onClick={()=>setZoom(z=>Math.min(2,z+.25))}><Plus size={16}/></button>
    </div>}
    {error&&<p role="alert" className="um-feedback failure">{error}</p>}
    {rendering&&<div role="status">PDF wird dargestellt …</div>}
    <div className="attachment-pdf-page" hidden={!!error||!document}>
      <canvas ref={canvas} aria-label={'PDF-Seite '+page}/>
    </div>
  </div>;
}
