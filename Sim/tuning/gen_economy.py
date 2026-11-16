import json, sys, math, os
P = dict(g=3.2, hp0=4.0, hpg=3.0, cg=3.2, c0=60.0,
         depth=[150,250,400,600,900,1300,1900,2800,4000,6000])
if len(sys.argv)>1: P.update(json.loads(sys.argv[1]))
g,hp0,hpg,cg,c0,depth = P['g'],P['hp0'],P['hpg'],P['cg'],P['c0'],P['depth']

items = [
 ("toprak","Toprak",["Paslı çivi","Eski düğme","Kırık çömlek","Solucan fosili","Eski anahtar","Bronz kolye","Gümüş sikke"]),
 ("kil","Kil",["Kil parçası","Kırık tuğla","Çatlak vazo","Deniz kabuğu","Pişmiş toprak heykelcik","Antik mühür","Eski altın küpe"]),
 ("tas","Taş",["Çakıl taşı","Mermer parçası","Fosil kabuğu","Kuvars parçası","Ametist parçası","Obsidyen bıçak","Akik taşı"]),
 ("komur","Kömür",["Kömür parçası","Kok kömürü","Antrasit","Kükürt","Parlak antrasit","Kehribar","Siyah elmas"]),
 ("demir","Demir",["Demir cevheri","Hematit","Pas parçası","Manyetit","Çelik cevheri","Meteor parçası","Altın damarlı demir"]),
 ("gumus","Gümüş",["Gümüş cevheri","Gümüş tozu","Eski gümüş kaşık","Gümüş sikke","Gümüş kolye","Saf gümüş külçe","Gümüş taç"]),
 ("altin","Altın",["Altın tozu","Altın kırıntısı","Altın taneciği","Pirit","Altın külçe","Altın sikke","Altın heykelcik"]),
 ("kristal","Kristal",["Kristal kırığı","Kuvars kristali","Mavi kristal","Pembe kristal","Parlayan kristal","Güç kristali","Kristal kalp"]),
 ("elmas","Elmas",["Ham elmas","Elmas tozu","Küçük elmas","Pırlanta kırığı","Mücevher","Safir","Kraliyet elması"]),
 ("kadim","Kadim Hazine",["Kadim sikke","Eski tapınak parçası","Kadim tılsım","Unutulmuş yüzük","Kadim mücevher","Altın tablet","Kadim taç"]),
]
rel = [(1,50,"common"),(1.5,40,"common"),(2,30,"common"),(3,20,"common"),(8,8,"uncommon"),(14,6,"uncommon"),(60,1.5,"rare")]
layers=[]
for i,(lid,name,its) in enumerate(items):
    m = g**i
    layers.append(dict(id=lid,name=name,depthBlocks=depth[i],blockHp=(P['hps'][i] if 'hps' in P else round(hp0*hpg**i,2)),
      items=[dict(name=n,value=max(1,round(v*m)),rarity=r,weight=w) for n,(v,w,r) in zip(its,rel)]))
def C(layer,k=1.0): return round((P['cb'][layer] if 'cb' in P else c0*cg**layer)*k)
pick = [dict(name="Tahta kazma",power=1,cost=0,minLayer=0),
        dict(name="Taş kazma",power=P.get('p1',3),cost=C(1,P.get('pk1',8)),minLayer=0),
        dict(name="Demir kazma",power=P.get('p2',9),cost=C(3,P.get('pk2',8)),minLayer=2),
        dict(name="Çelik kazma",power=P.get('p3',27),cost=C(5,P.get('pk3',8)),minLayer=4),
        dict(name="Elmas kazma",power=P.get('p4',80),cost=C(7,P.get('pk4',8)),minLayer=6)]
def U(id,name,desc,layer,k,mult,mx,eff,kind): return dict(id=id,name=name,desc=desc,baseCost=C(layer,k),costMult=mult,maxLevel=mx,effectPerLevel=eff,kind=kind,minLayer=max(0,layer-P.get('early',1)))
cm=P.get('cm',1.09); mx=P.get('mx',30); e=P.get('e',0.04); e0=P.get('e0',0.08); cm0=P.get('cm0',1.15); es=P.get('es',0.03)
ups=[]
pw=["Güçlü Kollar","Sağlam Eldiven","Çelik Bilek","Demir Yumruk","Maden Ustası","Kaya Kırıcı","Kristal Vuruş","Titan Kasları","Elmas Darbe","Kadim Güç"]
sl={0:"Pazarlık Ustalığı",3:"Tüccar Ağı",6:"Altın Borsası",8:"Kadim Tüccar"}
def pct(x): return str(round(x*100,1)).replace('.',',').rstrip('0').rstrip(',')
for i in range(10):
    ee = e0 if i==0 else e; cc = cm0 if i==0 else cm; mxi = mx
    if i>=P.get('tail',8): ee=P.get('et',0.04); cc=P.get('cmt',1.045); mxi=P.get('mxt',60)
    if i>=P.get('tail2',8): ee=P.get('et2',0.04); cc=P.get('cmt2',1.05); mxi=P.get('mxt2',80)
    ups.append(U("guc%d"%(i+1),pw[i],"Vuruş gücü her seviyede yüzde %s artar"%pct(ee),i,P.get('kp',0.25),cc,mxi,ee,"power"))
    if i in sl:
        ups.append(U("pazar%d"%(i+1),sl[i],"Satış geliri her seviyede yüzde %s artar"%pct(es),i,P.get('ks',0.8),cc,20,es,"sellBonus"))
ups += [
 U("krit1","Keskin Göz","Kritik vuruş şansı yüzde 2 artar",0,0.6,1.3,15,0.02,"crit"),
 U("krit2","Usta Vuruş","Kritik vuruş şansı yüzde 3 artar",4,0.6,1.3,10,0.03,"crit"),
 U("sans1","Şans Tılsımı","Nadir eşya şansı artar",0,0.5,1.2,30,0.15,"rareChance"),
 U("sans2","Define Haritası","Nadir eşya şansı çok artar",5,0.5,1.2,30,0.4,"rareChance"),
 U("canta1","Sırt Çantası","Çanta 10 eşya büyür",0,0.3,1.2,30,10,"bag"),
 U("canta2","Büyük Çanta","Çanta 40 eşya büyür",3,0.4,1.2,30,40,"bag"),
 U("canta3","Yük Arabası","Çanta 200 eşya büyür",6,0.4,1.2,30,200,"bag"),
 U("otosat","Otomatik Satış","Çanta dolunca kendiliğinden satar",2,3,2,1,1,"autosell"),
]
mach=[]
def M(id,name,kind,layer,k,interval,power):
    mach.append(dict(id=id,name=name,kind=kind,unlockCost=C(layer,k),minLayer=max(0,layer-P.get('early',1)),intervalSec=interval,power=power))
    pn = "Dinamit" if kind=="dynamite" else "Matkap"
    ups.append(U(id+"Power",name+" Gücü","Gücü her seviyede yüzde %s artar"%pct(P.get("mp",0.042)),layer,k*0.2,cm,10,P.get("mp",0.042),id+"Power"))
    ups.append(U(id+"Speed",name+" Hızı","Hızı her seviyede yüzde %s artar"%pct(P.get("msp",0.028)),layer,k*0.3,cm,10,P.get("msp",0.028),id+"Speed"))
mk=P.get('mk',4)
M("dynamite","Dinamit","dynamite",1,mk,P.get('di',20),P.get('dp',40))
M("drill","Matkap","drill",2,mk,P.get('ri',2),P.get('rp',3))
M("dynamite2","Güçlü Dinamit","dynamite",4,mk,P.get('di',20)*0.8,P.get('dp',40)*P.get('mg',20))
M("drill2","Güçlü Matkap","drill",5,mk,P.get('ri',2)*0.8,P.get('rp',3)*P.get('mg',20))
M("dynamite3","Mega Dinamit","dynamite",7,mk,P.get('di',20)*0.6,P.get('dp',40)*P.get('mg',20)**2)
M("drill3","Elmas Matkap","drill",8,mk,P.get('ri',2)*0.6,P.get('rp',3)*P.get('mg',20)**2)
out=dict(offlineCapSeconds=7200,autosaveSeconds=30,bagBase=20,critBase=0.05,critMultiplier=3,critCap=0.75,
  prestigeBonusPerRun=0.5,drillReportSeconds=6,treasureName="Kadim hazine sandığı",
  layers=layers,pickaxes=pick,upgrades=ups,machines=mach)
json.dump(out,open(os.path.join(os.path.dirname(os.path.abspath(__file__)),"..","..","Assets","StreamingAssets","economy.json"),"w"),ensure_ascii=False,indent=1)
print("ok",len(ups)+len(mach)+len(pick)-1,"shop items")
