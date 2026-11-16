import json,subprocess,sys,re,os
P=json.loads(sys.argv[1]); T=float(sys.argv[2]) if len(sys.argv)>2 else 17.5
iters=int(sys.argv[3]) if len(sys.argv)>3 else 10
w=[0.6,0.8,1,1.1,1.2,1.3,1.4,1.5,1.6,1.8]; sw=sum(w)
env=dict(os.environ,DOTNET_ROOT="/opt/dotnet",PATH="/opt/dotnet:"+os.environ["PATH"])
depth=P['depth']; cb=P.get('cb',[P['c0']*P['cg']**i for i in range(10)]); hps=P.get('hps',[round(P['hp0']*P['hpg']**i,2) for i in range(10)])
for it in range(iters):
    P['depth']=depth; P['hps']=hps; P['cb']=cb
    subprocess.run(["python3","gen_economy.py",json.dumps(P)],capture_output=True)
    out=subprocess.run("SIM_RAW=1 dotnet run --project Sim -c Release -- --seeds 1",shell=True,cwd=os.path.join(os.path.dirname(os.path.abspath(__file__)),"..",".."),capture_output=True,text=True,env=env).stdout
    arr=[float(x) for x in re.findall(r"^\s+\d+ .{16} ([\d.]+) sa",out,re.M)]
    m=re.search(r"Bitiş süresi: ([\d.]+)",out)
    if len(arr)<10 or not m:
        print("unfinished",out[-300:]); hps=[h*0.6 for h in hps]; continue
    inc=[float(x) for x in re.findall(r"^RAW \d+ \S+ (\S+)",out,re.M)]
    fin=float(m.group(1)); arr.append(fin)
    dur=[arr[i+1]-arr[i] for i in range(10)]
    print(it,"fin %.2f"%fin,[round(x,2) for x in dur],flush=True)
    new=[]
    for i in range(10):
        tgt=T*w[i]/sw
        if P.get('dmode'): new.append(max(30,round(depth[i]*(tgt/max(dur[i],0.01))**P.get('hg',0.6))))
        else: new.append(round(hps[i]*(tgt/max(dur[i],0.01))**P.get('hg',0.6),2))
    if P.get('dmode'): depth=new
    else: hps=new
    CK=P.get('ck',600)
    import math
    xs=[i for i in range(10) if inc[i]>0]; ys=[math.log(inc[i]) for i in xs]
    n=len(xs); mx_=sum(xs)/n; my=sum(ys)/n
    b=sum((x-mx_)*(y-my) for x,y in zip(xs,ys))/sum((x-mx_)**2 for x in xs); a=my-b*mx_
    if not P.get('freeze'): cb=[max(20.0,(cb[i]**0.6)*(CK*math.exp(a+b*i))**0.4) for i in range(10)]
P['depth']=depth; P['hps']=hps; P['cb']=[round(x) for x in cb]
print(json.dumps(P))
