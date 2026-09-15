const fs=require('fs'),zlib=require('zlib');
for(const count of [0,8,60]) {
 const pdf=fs.readFileSync(`.tmp-footer-${count}.pdf`).toString('latin1');
 const objects=new Map([...pdf.matchAll(/(\d+) 0 obj\s*([\s\S]*?)endobj/g)].map(m=>[m[1],m[2]]));
 const footerImages=[...objects].filter(([id,o])=>/\/Subtype \/Image\b/.test(o)&&/\/Width 206[2345]\b/.test(o)&&!/\/ColorSpace \/DeviceGray/.test(o)).map(([id])=>id);
 if(footerImages.length!==1)throw Error('Footer image count '+footerImages.length);
 const pages=[...objects.values()].filter(o=>/\/Type \/Page\b/.test(o));
 const matches=pages.map((o,i)=>o.includes(' '+footerImages[0]+' 0 R')?i+1:null).filter(Boolean);
 if(JSON.stringify(matches)!=='[1]')throw Error('Footer pages '+matches);
 const first=pages[0];const content=objects.get(first.match(/\/Contents (\d+) 0 R/)[1]);
 const compressed=content.match(/stream\r?\n([\s\S]*?)\r?\nendstream/)[1];
 const commands=zlib.inflateSync(Buffer.from(compressed,'latin1')).toString();
 fs.writeFileSync(`.tmp-footer-${count}-commands.txt`,commands);
 console.log(`${count} items: ${pages.length} pagina's; footerafbeelding uitsluitend op pagina 1.`);
}
