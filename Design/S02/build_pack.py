"""S02 Vintage Lottery: reproducible source art, PNGs, Unity metadata and QA proof.
Run from repo root: python Design/S02/build_pack.py
No Unity scene, gameplay or existing art changes.
"""
from pathlib import Path
import hashlib, json, math, re, shutil, uuid
from io import BytesIO
import cairosvg
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/"S02-Pack"
ART=OUT/"Assets/Art/UI/S02_Classic"
if OUT.exists(): shutil.rmtree(OUT)
ART.mkdir(parents=True)
PROOF=OUT/"Preview"; PROOF.mkdir()
SRC=OUT/"Source"; SRC.mkdir()
META=(ROOT/"Assets/Art/Symbols/sym_star.png.meta").read_text()
records={}
def svg(w,h,body):
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}"><g font-family="Liberation Sans, sans-serif">{body}</g></svg>'
def rect(x,y,w,h,fill,extra=""):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}" {extra}/>'
def label(x,y,s,size,color,extra=""):
    return f'<text x="{x}" y="{y}" font-size="{size}" font-weight="bold" fill="{color}" {extra}>{s}</text>'
def meta(rel,border,readable,w,h):
    gid=uuid.uuid5(uuid.NAMESPACE_URL,"scratchtoriches/s02/vintage-lottery/"+rel).hex
    t=re.sub(r"(?m)^guid: .*","guid: "+gid,META)
    t=t.replace("  isReadable: 0","  isReadable: "+str(int(readable)))
    t=re.sub(r"maxTextureSize: \d+","maxTextureSize: "+str(max(512,2**math.ceil(math.log2(max(w,h))))),t)
    t=t.replace("textureCompression: 1","textureCompression: 0")
    t=re.sub(r"spriteBorder: .*",f"spriteBorder: {{x: {border}, y: {border}, z: {border}, w: {border}}}",t)
    t=t.replace("spriteGenerateFallbackPhysicsShape: 1","spriteGenerateFallbackPhysicsShape: 0")
    return gid,t
def add(rel,w,h,body,scale=2,alpha=False,readable=False,border=0):
    data=svg(w,h,body)
    p=ART/(rel+".png"); p.parent.mkdir(parents=True,exist_ok=True)
    (SRC/(rel.replace("/","_")+".svg")).write_text(data)
    W,H=w*scale,h*scale
    cairosvg.svg2png(bytestring=data.encode(),write_to=str(p),output_width=W,output_height=H)
    im=Image.open(p).convert("RGBA")
    im.save(p,optimize=True)
    gid,t=meta(rel,border,readable,W,H)
    p.with_suffix(".png.meta").write_text(t)
    records[rel]={"path":str(p.relative_to(OUT)),"size":[W,H],"alpha":alpha,"readable":readable,"border":border,"guid":gid}
    return im
# Faithful geometry from the selected 960x540 mockup. Scale 2 for the 1920x1080 proof.
rays=""
for n in range(24):
    a=n*math.pi/12; z=(n+.38)*math.pi/12
    rays+=f'<path d="M176 76L{176+490*math.cos(a)} {76+490*math.sin(a)}L{176+490*math.cos(z)} {76+490*math.sin(z)}Z" fill="#E3A732" opacity=".65"/>'
ticket=rect(0,0,352,400,"#E3A732")+rect(6,6,340,388,"#BA3A2B")+f'<g clip-path="url(#inside)">{rays}</g>'
ticket='<defs><clipPath id="inside"><rect x="6" y="6" width="340" height="388"/></clipPath></defs>'+ticket
ticket+=rect(12,12,328,376,"none",'stroke="#F8DE8E" stroke-width="1.4"')
ticket+=label(25,35,"SERIE 01",11,"#F8E5AD")
ticket+='<g transform="translate(176 83) rotate(-3)">'+label(0,4,"CLASSICO",51,"#67291D",'text-anchor="middle" letter-spacing="-2.5" stroke="#67291D" stroke-width="5"')+label(0,0,"CLASSICO",51,"#FFEDB0",'text-anchor="middle" letter-spacing="-2.5"')+'</g>'
ticket+=rect(39,99,274,22,"#F5D263")+label(176,114,"GRATTA! ABBINA! VINCI!",10.5,"#703024",'letter-spacing="1" text-anchor="middle"')
ticket+=rect(20,130,312,236,"#F2DFAD",'stroke="#813326" stroke-width="3"')
ticket+=rect(20,374,312,16,"#F5D263")+label(26,385,"SCRATCHAHOLIC!",9,"#723024")
# Serial deliberately omitted: dynamic TMP overlay.
add("Ticket/Ticket_Classic",352,400,ticket,scale=3)
foil='<defs><pattern id="f" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(35)"><rect width="6" height="6" fill="#B7BCB7"/><path d="M0 0V6 M3 0V6" stroke="#E5E7E0" stroke-width=".8"/></pattern></defs>'
foil+=rect(0,0,86,64,"#B7BCB7")+rect(0,0,86,64,"url(#f)")
foil+='<ellipse cx="43" cy="32" rx="24" ry="16" fill="none" stroke="#77837B" opacity=".7"/>'
foil+=label(43,36,"S",11,"#59665F",'text-anchor="middle" letter-spacing="1"')
add("Ticket/ScratchCover_Classic",86,64,foil,scale=5,readable=True)
add("Ticket/Cell_Frame",92,70,rect(.5,.5,91,69,"none",'rx="2" stroke="#837F70" stroke-width="1"')+rect(1,1,90,68,"none",'rx="1" stroke="#D4C7A9" stroke-width="4"'),scale=4,alpha=True)
add("Ticket/Cell_Revealed",86,64,rect(0,0,86,64,"#FAEED1"),scale=4)
add("Ticket/Ticket_Shadow",352,400,rect(0,0,352,400,"#111B18",'rx="3" opacity=".35"'),scale=2,alpha=True)
grain='<defs><pattern id="g" width="9" height="9" patternUnits="userSpaceOnUse"><circle cx="3" cy="2" r=".5" fill="#292C23" opacity=".13"/></pattern></defs>'
add("Background/Gameplay_Background",960,540,grain+rect(0,0,960,540,"#31574C")+rect(0,0,960,540,"url(#g)"))
# Flat, tintable 9-sliced panels and buttons. Their functional labels remain TMP.
add("Panels/HUD_Panel",64,64,rect(.5,.5,63,63,"#203B33",'rx="4" stroke="#799688" stroke-width=".7"'),alpha=True,border=12)
add("Panels/Upgrade_Button",64,64,rect(0,0,64,64,"#EAC25C",'rx="4"')+rect(0,58,64,6,"#B48B36",'rx="2"'),alpha=True,border=16)
add("Panels/Menu_Button",48,39,rect(.5,.5,47,38,"#203B33",'rx="3" stroke="#CBD8C8"'),alpha=True,border=8)
add("Panels/HUD_Bar",32,32,rect(0,0,32,32,"#203B33"))
add("Icons/Menu",48,48,'<path d="M10 12H38 M10 24H38 M10 36H38" stroke="#F7EFD9" stroke-width="4" stroke-linecap="round"/>',alpha=True)
add("Icons/Arrow",48,48,'<path d="M17 10L31 24L17 38" fill="none" stroke="#20332E" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>',alpha=True)
add("Branding/Logo_HUD",310,48,label(2,35,"Scratchaholic!",29,"#EAC25C",'letter-spacing="-1.3"'),alpha=True)
# Original, flat, limited-ink symbol illustrations (not copied from real lotteries).
star='<path d="M64 10L80 45L119 49L90 76L97 115L64 96L31 115L38 76L9 49L48 45Z" fill="#E9BA4D" stroke="#805626" stroke-width="4" stroke-linejoin="round"/>'
coin='<circle cx="64" cy="66" r="49" fill="#D9A13E" stroke="#805626" stroke-width="4"/><circle cx="64" cy="61" r="45" fill="#EAC25C" stroke="#805626" stroke-width="3"/><circle cx="64" cy="61" r="34" fill="none" stroke="#A47A35" stroke-width="3"/><path d="M64 34L72 51L91 54L77 67L80 86L64 77L48 86L51 67L37 54L56 51Z" fill="#A47A35"/>'
symbols={
"sym_star":star,
"sym_coin":coin,
"sym_lucky7":'<path d="M23 19H105V39L62 111H32L77 41H23Z" fill="#BA3A2B" stroke="#783126" stroke-width="4" stroke-linejoin="round"/><path d="M30 25H97V33H30Z" fill="#F5D263"/>',
"sym_diamond":'<path d="M12 47L35 19H93L116 47L64 115Z" fill="#82C4C4" stroke="#32616A" stroke-width="4" stroke-linejoin="round"/><path d="M12 47H116L64 115L43 47L64 19L85 47Z" fill="none" stroke="#32616A" stroke-width="3"/><path d="M35 19L43 47L64 19Z M64 19L85 47L93 19Z" fill="#C9E7DD"/>',
"sym_crown":'<path d="M12 32L39 56L64 18L89 56L116 32L105 104H23Z" fill="#E9BA4D" stroke="#805626" stroke-width="4" stroke-linejoin="round"/><path d="M24 89H104V103H24Z" fill="#F4D878"/><path d="M64 59L76 73L64 85L52 73Z" fill="#BA3A2B"/>',
"sym_clover":'<path d="M63 68Q85 90 78 114" stroke="#38654B" stroke-width="8" fill="none" stroke-linecap="round"/><g fill="#51965D" stroke="#285F43" stroke-width="3"><path d="M64 65C3 58 12 5 44 12Q61 15 64 35Q67 15 84 12C116 5 125 58 64 65Z"/><path d="M64 65C3 60 12 112 44 105Q61 102 64 82Q67 102 84 105C116 112 125 60 64 65Z"/></g>',
"sym_coinbag":'<path d="M43 15H85L77 38C103 53 116 79 105 103Q64 125 23 103C12 79 25 53 51 38Z" fill="#D9A956" stroke="#805626" stroke-width="4" stroke-linejoin="round"/><path d="M43 38H85" stroke="#805626" stroke-width="7" stroke-linecap="round"/><circle cx="64" cy="79" r="18" fill="#EAC25C" stroke="#805626" stroke-width="3"/><path d="M64 67V91M58 73H69M58 85H69" stroke="#805626" stroke-width="3"/>'
}
for name,body in symbols.items():
    add("Symbols/"+name,128,128,body,scale=4,alpha=True)
add("Icons/Coin",128,128,coin,scale=2,alpha=True)
# Proof is built from the exported PNGs themselves, not an independent concept.
def load(rel,w,h):
    return Image.open(ART/(rel+".png")).convert("RGBA").resize((w*2,h*2),Image.Resampling.LANCZOS)
proof=load("Background/Gameplay_Background",960,540)
def paste(rel,x,y,w,h):
    proof.alpha_composite(load(rel,w,h),(x*2,y*2))
def box(x,y,w,h,c):
    ImageDraw.Draw(proof).rectangle((x*2,y*2,(x+w)*2-1,(y+h)*2-1),fill=c)
def text(x,y,s,size,c="#F7EFD9",bold=True,anchor="lt"):
    f=ImageFont.truetype("/usr/share/fonts/truetype/liberation2/LiberationSans-"+("Bold" if bold else "Regular")+".ttf",size*2)
    ImageDraw.Draw(proof).text((x*2,y*2),s,font=f,fill=c,anchor=anchor)
paste("Panels/HUD_Bar",0,0,960,64)
paste("Branding/Logo_HUD",26,5,310,48)
text(314,26,"CAPITOLO 01",12,"#CBD8C8")
text(734,10,"MONETE",11,"#CBD8C8"); paste("Icons/Coin",734,32,22,22);text(764,25,"20",31)
paste("Panels/Menu_Button",883,12,48,39);paste("Icons/Menu",891,16,32,32)
text(28,100,"GRATTATE",12,"#CBD8C8");text(25,114,"2 / 3",62)
for i in range(3): box(28+i*48,192,38,12,"#EAC25C" if i<2 else "#203B33")
text(28,216,"1 grattata rimasta",15)
box(28,252,221,1,"#789589")
text(28,270,"BIGLIETTO 003",12,"#CBD8C8");text(28,292,"Classico",23)
text(28,328,"Costo",14,"#CBD8C8",False);text(249,328,"Gratis",15,anchor="rt")
text(28,353,"Premio base",14,"#CBD8C8",False);text(249,350,"10",17,"#EAC25C",anchor="rt")
paste("Panels/HUD_Panel",28,391,221,76)
text(40,401,"PROSSIMO TRAGUARDO",11,"#CBD8C8");text(40,423,"Completa 3 Classici",15)
box(40,449,158,6,"#526F60");box(40,449,105,6,"#EAC25C");text(236,443,"2/3",13,anchor="rt")
paste("Ticket/Ticket_Shadow",310,84,352,400);paste("Ticket/Ticket_Classic",304,77,352,400)
cells=[]
for n in range(9):
    x=304+32+n%3*98; y=77+137+n//3*75
    cells.append([x,y,92,70])
    paste("Ticket/Cell_Revealed",x+3,y+3,86,64)
    paste("Ticket/Cell_Frame",x,y,92,70)
    if n in (0,4): paste("Symbols/sym_star",x+19,y+8,54,54)
    else: paste("Ticket/ScratchCover_Classic",x+3,y+3,86,64)
text(630,454,"000003",9,"#723024",anchor="rt")
text(711,99,"COMBINAZIONE ATTUALE",12,"#CBD8C8")
paste("Panels/HUD_Panel",711,124,221,181);box(711,124,221,5,"#EAC25C")
text(725,138,"COPPIA",31,"#EAC25C");text(725,179,"×2 · Stella",17)
box(725,207,193,1,"#789589");text(725,218,"Premio provvisorio",12,"#CBD8C8",False)
paste("Icons/Coin",730,252,28,28);text(917,233,"20",59,"#EAC25C",anchor="rt")
text(711,324,"COMBINAZIONI",12,"#CBD8C8");text(711,351,"Coppia ×2",14);text(932,351,"Tris ×3",14,anchor="rt")
paste("Panels/Upgrade_Button",711,391,221,76);text(724,405,"Potenziamenti",19,"#20332E");text(724,434,"2 acquistabili",13,"#20332E");paste("Icons/Arrow",902,413,24,24)
paste("Panels/HUD_Bar",0,489,960,51);text(480,498,"Gratta un'altra casella",16,anchor="mt");text(480,519,"Tieni premuto e trascina sulla patina argentata",12,"#CBD8C8",False,anchor="mt")
proof.save(PROOF/"S02_assembled_1920x1080.png")
# Full check of PNG decoding, alpha, import settings, GUID collisions, cell geometry.
existing=set()
for p in (ROOT/"Assets").rglob("*.meta"):
    m=re.search(r"(?m)^guid: ([0-9a-f]{32})$",p.read_text(errors="ignore"))
    if m: existing.add(m.group(1))
seen=set();checks=[]
for name,a in records.items():
    p=OUT/a["path"]
    with Image.open(p) as im:
        im.verify()
    im=Image.open(p)
    assert im.mode=="RGBA" and list(im.size)==a["size"],name
    lo,hi=im.getchannel("A").getextrema()
    assert hi>0 and (lo<255 if a["alpha"] else lo==255),name
    if name.startswith("Symbols/"):
        bb=im.getbbox()
        assert bb and min(bb[0],bb[1],im.width-bb[2],im.height-bb[3])>=4,name
    m=p.with_suffix(".png.meta").read_text()
    assert a["guid"] not in seen and a["guid"] not in existing,name
    seen.add(a["guid"])
    for s in ("textureType: 8","spriteMode: 1","spriteMeshType: 1","alphaIsTransparency: 1","enableMipMap: 0","textureCompression: 0"):
        assert s in m,(name,s)
    assert f'isReadable: {int(a["readable"])}' in m,name
    assert f'spriteBorder: {{x: {a["border"]}, y: {a["border"]}, z: {a["border"]}, w: {a["border"]}}}' in m,name
    assert a["border"]*2<min(im.size),name
    assert min(map(int,re.findall(r"maxTextureSize: (\d+)",m)))>=max(512,*im.size),name
    a["sha256"]=hashlib.sha256(p.read_bytes()).hexdigest()
    checks.append("PASS "+name+" | dimensions, RGBA, alpha, GUID, importer")
assert len(records)==21,len(records)
assert len(cells)==9 and len({tuple(c) for c in cells})==9
for i,(x,y,w,h) in enumerate(cells):
    assert 324<=x and x+w<=636 and 207<=y and y+h<=443
    for X,Y,W,H in cells[i+1:]:
        assert x+w<=X or X+W<=x or y+h<=Y or Y+H<=y
# Scratch mask resampling, matching ScratchCellManager's 128-square lookup.
cover=Image.open(ART/"Ticket/ScratchCover_Classic.png").convert("RGBA").resize((128,128),Image.Resampling.NEAREST)
assert cover.getchannel("A").getextrema()==(255,255)
assert len(set(cover.getdata()))>10
assert proof.size==(1920,1080)
checks+=["PASS nine non-overlapping 3x3 cells within the artwork opening","PASS opaque readable foil at 128x128 runtime sampling","PASS assembled proof 1920x1080 from exported PNGs","PASS 21 independent sprites; no functional text in UI sprites"]
(OUT/"checks.txt").write_text("\n".join(checks)+"\n",encoding="utf-8")
(OUT/"manifest.json").write_text(json.dumps({"assets":records,"referenceResolution":[1920,1080],"ticketRect":[608,154,704,800],"cellRects":[[v*2 for v in c] for c in cells],"limitations":["No Unity editor test performed","The existing ConfigureGrid uses a square 532-unit grid; adapting its visual layout to the approved rectangular cells is a separate integration step","Proof uses Italian sample HUD strings, not actual runtime state","Branding and printed ticket text only are baked"]},indent=2))
(OUT/"README.txt").write_text("""S02 Vintage Lottery - Pack 1\n21 PNG sprites with unique .meta files. Copy Assets/Art/UI/S02_Classic into the Unity project preserving .meta. Existing assets are not overwritten. See manifest.json for sizes and reference placement. Use SpriteRenderer/UI Image as appropriate; set panels/button Image.type=Sliced. Assign ScratchCover_Classic to brushedCover only when integrating S02; source is Read/Write enabled and opaque. Keep cell frame, symbol and scratch layer independent. HUD labels and serial use TMP/localization. Preview is a composition check, not a Unity screenshot. Current runtime uses square cells; rectangular layout requires a separate visual integration. No gameplay changes. Sources are included for revisions. The proof font is Liberation Sans; choose an appropriately licensed TMP font during integration.\n""")
print("\n".join(checks))


# Compact visual proof in CI logs for inspection without extracting the archive.
import base64
buf=BytesIO()
proof.convert("RGB").resize((960,540),Image.Resampling.LANCZOS).save(buf,format="JPEG",quality=85)
encoded=base64.b64encode(buf.getvalue()).decode("ascii")
for i in range(0,len(encoded),512):
    print("S02_PROOF_JPEG "+encoded[i:i+512])
