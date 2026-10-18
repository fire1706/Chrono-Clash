// Rend les dessins du prototype HTML (troupes et chefs) en images PNG pour Godot.
const {JSDOM}=require('jsdom');const fs=require('fs');const {createCanvas,ImageData}=require('@napi-rs/canvas');
const html=fs.readFileSync(__dirname+'/prototype.html','utf8');
const dom=new JSDOM(html,{runScripts:'outside-only',pretendToBeVisual:true,url:'https://x.test/'});const w=dom.window;
const mk=()=>new Proxy(function(){},{get:(t,k)=>k==='measureText'?()=>({width:10}):k==='createImageData'?(a,b)=>({data:new Uint8ClampedArray(a*b*4)}):k==='createRadialGradient'||k==='createLinearGradient'?()=>({addColorStop(){}}):mk(),apply:()=>mk(),set:()=>true});
w.HTMLCanvasElement.prototype.getContext=()=>mk();w.HTMLCanvasElement.prototype.toDataURL=()=>'data:,';
const src=html.match(/<script>([\s\S]*)<\/script>/)[1];
w.eval(src+';window.__D={drawUnitBody,drawGeneral};');
const {drawUnitBody,drawGeneral}=w.__D;
const out=__dirname+'/out';fs.mkdirSync(out+'/units',{recursive:true});fs.mkdirSync(out+'/chiefs',{recursive:true});
// Taille de l'image et étendue représentée dans le monde (pixels du jeu).
const SPEC={war:{px:96,world:46},rng:{px:96,world:46},tank:{px:128,world:64},chief:{px:128,world:72}};
const KIND={war:'war',rng:'rng',tank:'tank'};
function render(kind,era,teamColor,isChief){
  const sp=SPEC[isChief?'chief':kind],S=sp.px/sp.world;
  const cv=createCanvas(sp.px,sp.px),c=cv.getContext('2d');
  // On ignore l'ombre au sol dessinée par le prototype : le jeu ajoute la sienne.
  const fill=c.fill.bind(c);c.fill=(...a)=>{const f=String(c.fillStyle);if(/10,\s*15,\s*5/.test(f))return;fill(...a);};
  c.translate(sp.px/2,sp.px/2);c.scale(S,S);
  const army={color:teamColor,id:0};
  if(isChief) drawGeneral({era,ang:0,vx:0,vy:0,walk:0,swing:0,promoted:false,army,evoFlash:0,flash:0},c,{x:0,y:0},teamColor,false);
  else drawUnitBody(c,{t:kind,era,ang:0,vx:0,vy:0,walk:0,swing:0,flash:0,army});
  return c.getImageData(0,0,sp.px,sp.px);
}
function save(img,file){const cv=createCanvas(img.width,img.height);cv.getContext('2d').putImageData(img,0,0);fs.writeFileSync(file,cv.toBuffer('image/png'));}
function bbox(img){let x0=1e9,y0=1e9,x1=-1,y1=-1;const d=img.data;for(let y=0;y<img.height;y++)for(let x=0;x<img.width;x++)if(d[(y*img.width+x)*4+3]>8){x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}return [x0,y0,x1,y1];}
const report=[];
for(const [kind,isChief] of [['war',false],['rng',false],['tank',false],['chief',true]]){
  for(let era=0;era<8;era++){
    const name=isChief?`chief_${era}`:`${kind}_${era}`,dir=isChief?'chiefs':'units';
    const base=render(kind,era,'#8a8a8a',isChief);          // zones du clan en gris neutre
    const a=render(kind,era,'#ff00ff',isChief),b=render(kind,era,'#00ff00',isChief);
    // Masque : là où les deux rendus diffèrent, c'est la couleur du clan → blanc.
const m={width:a.width,height:a.height,data:new Uint8ClampedArray(a.data.length)};
    let teamPixels=0;
    for(let i=0;i<a.data.length;i+=4){const diff=Math.abs(a.data[i]-b.data[i])+Math.abs(a.data[i+1]-b.data[i+1])+Math.abs(a.data[i+2]-b.data[i+2]);
      if(diff>60){m.data[i]=m.data[i+1]=m.data[i+2]=255;m.data[i+3]=Math.min(255,Math.round(a.data[i+3]*Math.min(1,diff/400)));teamPixels++;}}
    save(base,`${out}/${dir}/${name}.png`);save(new ImageData(m.data,m.width,m.height),`${out}/${dir}/${name}_team.png`);
    const bb=bbox(base),sp=SPEC[isChief?'chief':kind];
    const clipped=bb[0]<=0||bb[1]<=0||bb[2]>=sp.px-1||bb[3]>=sp.px-1;
    report.push(`${name}: bbox ${bb.join(',')} / ${sp.px}px, team px ${teamPixels}${clipped?'  ⚠ COUPÉ':''}`);
  }
}
console.log(report.join('\n'));
process.exit(0);
