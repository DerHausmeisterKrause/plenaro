import {cpSync,mkdirSync} from 'node:fs';
mkdirSync('dist/pdfjs',{recursive:true});
for(const dir of ['cmaps','standard_fonts']){
  cpSync('node_modules/pdfjs-dist/'+dir,'dist/pdfjs/'+dir,{recursive:true});
}
