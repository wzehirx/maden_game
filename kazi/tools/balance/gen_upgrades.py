import json, sys
import os
HERE=os.path.dirname(os.path.abspath(__file__))
OUT=os.path.join(HERE,'..','..','data')+'/'
P=json.load(open('params.json'))
UO=P.get('u',{})
U=[]
def up(id,name,desc,cat,kind,base,mult,mx,eff,req="",mc=1):
    b=UO.get(id,{})
    sc=P.get('us',[1,1,1,1,1])[mc-1]
    base=base*sc
    if mx>=8 and kind not in('AutoUnlock','AutoBuy','ScratchPower','Press','AutoTypes'):
        f=P.get('lvf',1.6); nm=int(round(mx*f)); mult=mult**(mx/nm); eff=eff*mx/nm; mx=nm
    U.append(dict(id=id,name=name,desc=desc,category=cat,kind=kind,baseCost=round(max(P.get('minbase',12),b.get('base',base)),2),costMult=round(b.get('mult',mult),4),maxLevel=b.get('max',mx),effect=round(b.get('eff',eff),5),requires=req,minCatalog=mc))
# Kazıma
up("guc","Kazıma Gücü","Her dokunuşta bir hücre daha açar.","scratch","ScratchPower",25,2.0,8,1)
up("sans1","Şans I","Kazanma ihtimali biraz artar.","scratch","Luck",40,1.6,10,0.5)
up("sans2","Şans II","Kazanma ihtimali ve nadir semboller artar.","scratch","Luck",3000,1.7,10,1.0,"",2)
up("sans3","Şans III","Şans daha da artar.","scratch","Luck",120000,1.7,10,1.5,"",3)
up("sans4","Şans IV","Büyük şans.","scratch","Luck",5000000,1.7,10,2.0,"",4)
up("sans5","Şans V","Altın şans.","scratch","Luck",200000000,1.7,10,2.5,"",5)
up("carp1","Kazanç Çarpanı I","Ödemeler yüzde on artar.","scratch","Payout",60,1.7,10,0.05)
up("carp2","Kazanç Çarpanı II","Ödemeler yüzde otuz artar.","scratch","Payout",5000,1.7,10,0.15,"",2)
up("carp3","Kazanç Çarpanı III","Ödemeler iki kat gibi artar.","scratch","Payout",200000,1.7,10,0.4,"",3)
up("carp4","Kazanç Çarpanı IV","Ödemeler çok artar.","scratch","Payout",8000000,1.7,10,1.0,"",4)
up("carp5","Kazanç Çarpanı V","Ödemeler dev gibi artar.","scratch","Payout",300000000,1.7,10,2.0,"",5)
up("indirim1","Bilet İndirimi","Bilet fiyatları yüzde iki düşer.","scratch","Discount",100,1.45,10,0.02)
up("indirim2","Toptan Alım","Bilet fiyatları yüzde iki daha düşer.","scratch","Discount",100000,1.5,10,0.02,"",3)
up("cuzdan","Cüzdan Fermuarı","Bilet fiyatları yüzde bir düşer.","scratch","Discount",1500,1.5,10,0.01)
up("kasa_anahtari","Kasa Anahtarı","Ödemeler yüzde iki artar.","scratch","Payout",2500,1.5,10,0.02)
up("kupon","Altın Kupon","Ödemeler yüzde üç artar.","scratch","Payout",6000,1.5,10,0.03)
up("talih","Talih Tılsımı","Şans biraz daha artar.","scratch","Luck",9000,1.5,10,0.3)
up("canta","Çanta","Çantaya bir kart daha sığar.","scratch","Bag",150,1.5,9,1)
up("cevrimdisi","Uyku Çantası","Çevrimdışı kazanç süresi on dakika uzar.","scratch","Offline",500,1.8,11,600,"",2)
up("iade","Sağlam Kâğıt","Çöpe atılan kartın iadesi artar.","scratch","Refund",2000,1.8,6,0.05,"",2)
# Otomasyon
up("oto_kaziyici","Otomatik Kazıyıcı","Çantadaki kartları kendisi kazır.","auto","AutoUnlock",1200,1.0,1,1)
up("oto_hiz","Kazıyıcı Hızı","Dakikada altı kart daha kazır.","auto","AutoSpeed",1800,1.5,12,3,"oto_kaziyici")
up("oto_kuyruk","Kuyruk Uzunluğu","Kuyruğa üç kart daha sığar.","auto","AutoQueue",2000,1.5,10,3,"oto_kaziyici")
up("oto_alim","Otomatik Alım","Bakiye yettikçe kuyruğa kart alır.","auto","AutoBuy",4000,1.0,1,1,"oto_kaziyici")
up("oto_tur","Tür Seçici","Otomatik alım için daha pahalı kart türleri seçilebilir.","auto","AutoTypes",5000,6.0,5,1,"oto_alim",2)
up("oto_hiz2","Kazıyıcı Hızı II","Dakikada on beş kart daha kazır.","auto","AutoSpeed",80000,1.5,8,6,"oto_kaziyici",3)
up("oto_kuyruk2","Kuyruk Uzunluğu II","Kuyruğa sekiz kart daha sığar.","auto","AutoQueue",100000,1.5,6,8,"oto_kaziyici",3)
up("oto_hiz3","Kazıyıcı Hızı III","Dakikada kırk kart daha kazır.","auto","AutoSpeed",20000000,1.6,8,8,"oto_kaziyici",5)
# Gadget
up("sayac","Mekanik Sayaç","Otomatik kazıyıcıyı yüzde yirmi beş hızlandırır.","gadget","Counter",8000,1.8,10,0.10,"oto_kaziyici")
up("mercek","Mercek","Tuzakları daha iyi sezer, otomatik kazıyıcı daha çok hücreye göz atar.","gadget","Lens",20000,3.0,4,1,"oto_kaziyici",2)
up("cikrik","Şans Çıkrığı","Şansı artırır.","gadget","Luck",50000,1.7,10,0.5,"oto_kaziyici",2)
up("pres","Dişli Pres","Tek turda bir kart daha işler.","gadget","Press",300000,4.0,3,1,"oto_kaziyici",3)
up("zil","Kasa Zili","Ödemelere küçük bonus ekler.","gadget","Payout",100000,1.8,10,0.04,"oto_kaziyici",2)
up("yonca","Dört Yapraklı Yonca","Şansı çok artırır.","gadget","Luck",10000000,1.8,8,1.0,"oto_kaziyici",4)
up("zil2","Altın Zil","Ödemelere büyük bonus ekler.","gadget","Payout",20000000,1.8,10,0.25,"oto_kaziyici",4)
up("pres2","Dişli Pres II","Tek turda bir kart daha işler.","gadget","Press",5000000000,4.0,1,1,"pres",5)
cum=[0]*5
for u in U: cum[u['minCatalog']-1]+=u['maxLevel']
import itertools
cs=list(itertools.accumulate(cum)); fr=P.get('glf',[0.76,0.93,0.96,0.98,0.98])
json.dump([int(c*f) for c,f in zip(cs,fr)],open('gl.json','w'))
json.dump(dict(upgrades=U),open(OUT+'upgrades.json','w'),ensure_ascii=False,indent=1)

NO=P.get('n',{})
N=[]
def nd(id,name,desc,cost,kind,val,req=""):
    N.append(dict(id=id,name=name,desc=desc,cost=max(1,int(round(NO.get(id,{}).get('cost',cost)*P.get('ncs',1.0)))),kind=kind,value=NO.get(id,{}).get('value',val),requires=req))
nd("n_start1","Cep Harçlığı","Her koşuya beş bin lirayla başla.",2,"StartMoney",5000)
nd("n_luck1","Kalıcı Şans I","Başlangıç şansı beş artar.",3,"Luck",5)
nd("n_pay1","Altın Eller","Ödemeler yüzde yirmi beş artar.",4,"Payout",0.25)
nd("n_disc1","Pazarlık","Bilet fiyatları yüzde üç düşer.",3,"Discount",0.03)
nd("n_power1","Hızlı Parmaklar","Kazıma gücü bir artar.",3,"ScratchPower",1)
nd("n_off1","Hafif Uyku","Çevrimdışı süre yirmi dakika uzar.",3,"Offline",1200)
nd("n_auto","Erken Kazıyıcı","Otomatik kazıyıcı koşu başında hazır.",6,"StartAuto",1,"n_start1")
nd("n_start2","Dolu Cüzdan","Her koşuya bir milyon lirayla başla.",6,"StartMoney",1000000,"n_start1")
nd("n_luck2","Kalıcı Şans II","Başlangıç şansı on artar.",7,"Luck",10,"n_luck1")
nd("n_pay2","Altın Parmaklar","Ödemeler yüzde yetmiş beş artar.",8,"Payout",0.75,"n_pay1")
nd("n_ucd1","Esnaf İndirimi","Yükseltmeler yüzde on ucuzlar.",8,"UpgradeDiscount",0.10,"n_disc1")
nd("n_speed1","Yağlı Dişliler","Kazıyıcı dakikada on kart hızlanır.",7,"AutoSpeed",10,"n_auto")
nd("n_help","Cömert Yardım","Yardım parası yirmi lira artar.",4,"HelpMoney",20,"n_power1")
nd("n_buy","Erken Alım","Otomatik alım koşu başında hazır.",15,"AutoBuy",1,"n_auto")
nd("n_ab1","Altın Fırsat","Altın Bilet puanı yüzde yirmi beş artar.",12,"AbGain",0.25,"n_pay1")
nd("n_start3","Kasa Dolusu","Her koşuya yüz milyon lirayla başla.",12,"StartMoney",100000000,"n_start2")
nd("n_luck3","Kalıcı Şans III","Başlangıç şansı yirmi artar.",14,"Luck",20,"n_luck2")
nd("n_pay3","Altın Kasa","Ödemeler iki kat gibi artar.",16,"Payout",2.0,"n_pay2")
nd("n_ucd2","Toptancı","Yükseltmeler yüzde on beş daha ucuzlar.",16,"UpgradeDiscount",0.15,"n_ucd1")
nd("n_press","Çift Dişli","Tek turda bir kart daha işlenir.",14,"Press",1,"n_speed1")
nd("n_cat2","Çarşıdan Başla","Çarşı Pazarı koşu başında açık.",20,"StartCatalog",2,"n_start3")
nd("n_cat3","Handan Başla","Han Köşkü koşu başında açık.",30,"StartCatalog",3,"n_cat2")
nd("n_ab2","Altın Mıknatıs","Altın Bilet puanı yüzde elli artar.",25,"AbGain",0.5,"n_ab1")
nd("n_pay4","Altın Çağ","Ödemeler altı kat artar.",30,"Payout",6.0,"n_pay3")
json.dump(dict(nodes=N),open(OUT+'prestige.json','w'),ensure_ascii=False,indent=1)

S=dict(helpMoney=10,discardRefundPct=0.4,offlineCapSeconds=7200,autosaveSeconds=30,startMoney=20,
 scratchThresholds=dict(open=0.55,peek=0.25),luckWinScale=0.05,luckSymbolScale=0.02,luckTrapScale=0.03,
 rarityMults=[2,10,50],autoBaseCpm=6,baseBag=1,baseOfflineSeconds=600,maxDiscount=0.5,maxRefund=0.9,
 affordableCooldownSeconds=15,autoReportSeconds=60,autoPeekCells=3,abBase=5,abRefEarned=100000,abExponent=0.5,
 minUpgradeCostMult=0.3,autoBufferFactor=2)
S.update(P.get('s',{}))
json.dump(S,open(OUT+'settings.json','w'),ensure_ascii=False,indent=1)
