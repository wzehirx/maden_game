import json, math, sys
import os
HERE=os.path.dirname(os.path.abspath(__file__))
OUT=os.path.join(HERE,'..','..','data')+'/'
P=json.load(open('params.json')) if len(sys.argv)>1 else {}

def sym(id,name,w,pay,kind="Normal"): return dict(id=id,name=name,weight=w,pay=pay,kind=kind)
NUM=[("n1","bir",30,1.2),("n2","iki",25,1.5),("n3","üç",20,2),("n4","dört",15,3),("n5","beş",10,5),("n6","altı",6,8),("n7","yedi",3,12),("n8","sekiz",2,30)]
def nums(scale=1.0): return [sym(i,n,w,round(p*scale,3)) for i,n,w,p in NUM]
def E1(syms):
    ws=[s for s in syms if s['kind']=='Normal' and s['pay']>0]
    W=sum(s['weight'] for s in ws); return sum(s['weight']*s['pay'] for s in ws)/W
def E2(syms):
    ws=[s for s in syms if s['kind']=='Normal' and s['pay']>0]; W=sum(s['weight'] for s in ws); e=0
    for a in ws:
        rest=W-a['weight']; e+=a['weight']/W*sum(b['weight']/rest*b['pay'] for b in ws if b is not a)
    return e
def EM(m): return sum(x['weight']*x['mult'] for x in m)/sum(x['weight'] for x in m)
MULT=[dict(mult=1,weight=50),dict(mult=2,weight=30),dict(mult=3,weight=12),dict(mult=5,weight=6),dict(mult=10,weight=2)]
TRAPS=[sym("akrep","Akrep",1,1.0,"Trap"),sym("kara_bulut","Kara Bulut",2,0.5,"Trap")]
def trap_pen(t,tdc): 
    tw=sum(s['weight'] for s in TRAPS); avg=sum(s['weight']*s['pay'] for s in TRAPS)/tw
    return t*(1+tdc)*avg

types=[]
def add(id,name,cat,rule,price,syms,rtp,**kw):
    d=dict(id=id,name=name,catalog=cat,rule=rule,price=price,symbols=syms)
    d.update({k:v for k,v in kw.items() if k not in('mult',)})
    n=len(syms)
    if rule in('ThreeOfAKind','Jackpot','Joker','Sequence'): e=E1(syms)
    elif rule=='NumberHunt': e=E1(syms)+kw.get('doubleChance',0)*E2(syms)
    elif rule=='Multiplier': e=E1(syms)*EM(kw['multipliers'])
    elif rule=='Trap': e=E1(syms); 
    if rule=='Trap': wc=(rtp+trap_pen(kw['trapChance'],kw['trapDoubleChance']))/e
    else: wc=rtp/e
    d['winChance']=round(wc,4); d['maxWinChance']=round(min(0.8,max(wc*2.5,wc+0.1)),3)
    assert wc<0.85,(id,wc)
    types.append(d)

R=P.get('rtp',{})
def r(k,dflt): return R.get(k,dflt)
pr=P.get('price',{})
def pz(k,d): return pr.get(k,d)
US=P.get('us',[1,1,1,1,1]); UNIT=[0,0,4000,120000,5000000,200000000]
def G(k):
    if k==1: return int(P.get('g1',2500))
    return int(P.get('gates',[800,20000,800000,60000000,1200000000])[k-1])

toak1=[sym("simit","Simit",40,1.5),sym("cay","Çay Bardağı",30,2),sym("guvercin","Güvercin",18,3),sym("marti","Martı",8,6),sym("vapur","Vapur",4,12),sym("lale","Lale",2,25)]
add("simitci","Simitçi",1,"ThreeOfAKind",pz('simitci',10),toak1,r('simitci',1.25))
add("cay_bardagi","Çay Bardağı Kartı",1,"NumberHunt",pz('cay_bardagi',30),nums(),r('cay_bardagi',1.2),doubleChance=0.15)

def gate(id,name,cat,rule,price,pay,p,pity,decoys):
    s=[sym("anahtar","Anahtar",1,pay,"Gate")]+[sym(i,n,1,0) for i,n in decoys]
    types.append(dict(id=id,name=name,catalog=cat,rule=rule,price=price,symbols=s,winChance=p,maxWinChance=min(0.9,round(p*2.2,3)),pityAfter=pity))
dec=[("kilit","Kilit"),("cira","Çıra"),("defter","Defter"),("tespih","Tespih"),("muhur","Mühür")]
gate("mahalle_sonsans","Mahalle Son Şansı",1,"SonSans",G(1),3.4,0.30,4,dec)

mul2=[sym("lale","Lale",35,1.5),sym("fener","Fener",28,2),sym("kilim","Kilim",18,3),sym("nazar","Nazar Boncuğu",10,6),sym("cini","Çini",5,15),sym("zumrut","Zümrüt",1,60)]
add("lale_tezgahi","Lale Tezgâhı",2,"Multiplier",pz('lale_tezgahi',400),mul2,r('lale_tezgahi',1.2),multipliers=MULT)
tr=[sym("kapi","Kapı",35,1.5),sym("fener","Fener",28,2),sym("sandik","Sandık",18,3),sym("ay","Ay",10,6),sym("yildiz","Yıldız",5,15),sym("tac","Taç",1,60)]+TRAPS
add("fener_ustasi","Fener Ustası",2,"Trap",pz('fener_ustasi',1000),tr,r('fener_ustasi',1.2),trapChance=0.28,trapDoubleChance=0.25)
gate("carsi_sonsans","Çarşı Son Şansı",2,"SonSans",G(2),3.4,0.30,4,dec)

jk=[sym("kehribar","Kehribar",35,1.5),sym("mercan","Mercan",28,2),sym("inci","İnci",18,3),sym("zeytin","Zeytin",12,5),sym("baharat","Baharat",6,12),sym("kervan","Kervan",3,30)]
add("kervan_jokeri","Kervan Jokeri",3,"Joker",pz('kervan_jokeri',15000),jk+[sym("joker","Joker",4,0,"Joker")],r('kervan_jokeri',1.15),jokerChance=0.35)
sq=[sym("kilim1","Kilim",35,1.5),sym("sedef","Sedef",28,2),sym("seccade","Seccade",18,3),sym("ibrik","İbrik",10,6),sym("hali","Halı",5,15),sym("sultan","Sultan",1,60)]
add("kilim_deseni","Kilim Deseni",3,"Sequence",pz('kilim_deseni',40000),sq,r('kilim_deseni',1.15))
gate("han_sonsans","Han Son Şansı",3,"SonSans",G(3),3.4,0.30,4,dec)

ja=[sym("yali","Yalı",40,1.5),sym("martik","Martı",30,2),sym("vapur2","Vapur",18,3),sym("kule","Kule",8,6),sym("kopru","Köprü",3,15),sym("hilal","Altın Hilal",0.6,150)]
add("bogaz_yalisi","Boğaz Yalısı",4,"Jackpot",pz('bogaz_yalisi',600000),ja,r('bogaz_yalisi',1.15))
add("saray_sayisi","Saray Sayısı",4,"NumberHunt",pz('saray_sayisi',1500000),nums(),r('saray_sayisi',1.15),doubleChance=0.2)
tr4=[sym("ferman","Ferman",35,1.5),sym("mühür","Mühür",28,2),sym("kilic","Kılıç",18,3),sym("tahtt","Taht",10,6),sym("elmas","Elmas",5,15),sym("hazine","Hazine",1,60)]+TRAPS
for s in tr4:
    if s['id']=="mühür": s['id']="muhur2"
    if s['id']=="tahtt": s['id']="taht"
add("divan_tuzagi","Divan Tuzağı",4,"Trap",pz('divan_tuzagi',3000000),tr4,r('divan_tuzagi',1.15),trapChance=0.3,trapDoubleChance=0.25)
gate("saray_sonsans","Saray Son Şansı",4,"SonSans",G(4),3.4,0.30,4,dec)

m5=[sym("altin","Altın",35,1.5),sym("gumus","Gümüş",28,2),sym("yakut","Yakut",18,3),sym("safir","Safir",10,6),sym("pirlanta","Pırlanta",5,15),sym("tac2","Taç",1,60)]
add("altin_kosk","Altın Köşk Çarpanı",5,"Multiplier",pz('altin_kosk',25000000),m5,r('altin_kosk',1.15),multipliers=MULT)
j5=[sym("define","Define",40,1.5),sym("harita","Harita",30,2),sym("pusula","Pusula",18,3),sym("sandik5","Sandık",8,6),sym("kese","Kese",3,15),sym("hazine5","Büyük Hazine",0.6,200)]
add("define_haritasi","Define Haritası",5,"Jackpot",pz('define_haritasi',60000000),j5,r('define_haritasi',1.15))
gate("altin_bilet","Altın Bilet Son Şansı",5,"AltinBilet",pz('g5',G(5)),3.4,0.30,4,dec)

GL=json.load(open("gl.json"))
cats=[dict(id=i+1,name=n,gateLevels=GL[i]) for i,n in enumerate(["Mahalle Bayii","Çarşı Pazarı","Han Köşkü","Saray Köşkü","Altın Köşk"])]
json.dump(dict(catalogs=cats,cardTypes=types),open(OUT+'cards.json','w'),ensure_ascii=False,indent=1)
for t in types: print(t['id'],t['price'],t.get('winChance'))
