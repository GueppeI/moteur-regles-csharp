"""Résumé de la couverture de code (fichier Cobertura produit par coverlet), par projet et par classe.

Usage : python3 outils/couverture.py TestResults/*/coverage.cobertura.xml
"""
import sys, xml.etree.ElementTree as ET, collections
root=ET.parse(sys.argv[1]).getroot()
print(f"Global : lignes {float(root.get('line-rate'))*100:.1f} %  |  branches {float(root.get('branch-rate'))*100:.1f} %")
print()
for pkg in root.iter('package'):
    print(f"## {pkg.get('name')} : lignes {float(pkg.get('line-rate'))*100:.1f} %, branches {float(pkg.get('branch-rate'))*100:.1f} %")
    agg=collections.OrderedDict()
    for c in pkg.iter('class'):
        name=c.get('name').split('<')[0].split('/')[0]
        lines=c.find('lines')
        a=agg.setdefault(name,[0,0,set()])
        for l in lines.iter('line'):
            a[0]+=1; a[1]+= int(l.get('hits'))>0
            if int(l.get('hits'))==0: a[2].add((c.get('filename').split('/')[-1], int(l.get('number'))))
    for n,(t,h,miss) in agg.items():
        if t==0: continue
        m = ', '.join(f"{f}:{ln}" for f,ln in sorted(miss)) if miss else ''
        print(f"   {n:<62} {h:>3}/{t:<3} {h/t*100:5.1f} %  {('non couvert: '+m) if m else ''}")
    print()
