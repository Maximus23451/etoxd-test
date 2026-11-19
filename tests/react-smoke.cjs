const {chromium}=require('playwright');
const assert=require('node:assert/strict');
(async()=>{
  const {startFixture}=await import('./fixture.js');const f=await startFixture(3003);
  let browser;const errors=[];
  try {
    browser=await chromium.launch({headless:true,executablePath:process.env.CHROMIUM_PATH || '/usr/bin/chromium'});
    async function context(id,width=1440){const c=await browser.newContext({viewport:{width,height:1000},timezoneId:'Europe/Budapest'});const p=await c.newPage();p.on('pageerror',e=>errors.push(e.message));await p.goto(f.base);await p.locator(`[data-demo-id="${id}"]`).click();await p.getByRole('heading',{name:'A heted a pályán'}).waitFor();return {p,c};}
    {
      const {p,c}=await context('TX-0001');await p.getByRole('link',{name:'Szerepkörök',exact:true}).click();await p.getByPlaceholder('Pl. Korosztály-koordinátor').fill('Mobile coordinator');await p.getByLabel('Korosztálytagságok kezelése az egész klubban').check();await p.getByRole('button',{name:'Szerepkör mentése'}).click();await p.locator('.role-list').getByText('Mobile coordinator',{exact:true}).waitFor();
      await p.getByRole('link',{name:'Állapotteszt'}).click();await p.getByRole('button',{name:'Betöltési animáció'}).click();await p.getByRole('status').filter({hasText:'Tesztválaszra várunk'}).waitFor();await p.getByText('✓ A művelet sikerült.').waitFor();
      for(const code of [401,403,404,500]){await p.getByRole('button',{name:new RegExp('^'+code+' ·')}).click();await p.locator('.error-code').filter({hasText:String(code)}).waitFor();assert(await p.getByText('Tesztállapot · az adataid nem változtak').isVisible());}
      await p.getByRole('link',{name:'Eseménynapló'}).click();await p.locator('.activity-list article').first().waitFor();assert(await p.getByText('diagnostics.test',{exact:true}).count()>0);
      await p.screenshot({path:'/tmp/eto-react-manager.png'});await c.close();console.log('PASS manager roles, real API error pages, loading animation, activity log');
    }
    {
      const {p,c}=await context('TX-9001');await p.getByRole('link',{name:'Játékosok & korosztályok'}).click();await p.getByPlaceholder('Pl. Józsi').fill('Józsi');await p.getByRole('button',{name:'Korosztályok módosítása'}).click();await p.getByRole('checkbox',{name:'U17',exact:true}).check();await p.getByRole('button',{name:'Tagságok mentése'}).click();await p.getByRole('dialog').waitFor({state:'hidden'});assert.match(await p.locator('.membership-badges').innerText(),/U15/);assert.match(await p.locator('.membership-badges').innerText(),/U17/);
      await p.getByRole('link',{name:'Naptár',exact:true}).click();const day=new Date(Date.now()+86400000).toISOString().slice(0,10);await p.locator('input[type="file"]').setInputFiles({name:'weekly.csv',mimeType:'text/csv',buffer:Buffer.from('Dátum;Kezdés;Vége;Csapat;Edzés;Helyszín;Edző\n'+day+';16:00;17:30;U17;Imported browser training;Pitch B;Coach Test')});await p.getByRole('button',{name:'Beosztás importálása'}).click();await p.getByText('1 edzés importálva.',{exact:false}).waitFor();await p.getByRole('button',{name:/Imported browser training/}).first().click();assert(await p.getByText('Edző: Coach Test',{exact:false}).count()>0);await p.getByRole('button',{name:'Bezárás',exact:true}).click();
      await p.getByRole('button',{name:'Kijelentkezés'}).click();await p.locator('#talentx').fill('TX-1501');await p.locator('#password').fill('eto1904');await p.getByRole('button',{name:'BELÉPEK →'}).click();await p.getByRole('heading',{name:'A heted a pályán'}).waitFor();assert.equal(await p.locator('#group-filter option').count(),3);await c.close();console.log('PASS multi-group membership UI, CSV import/calendar details, player sees U15 and U17');
    }
    for(const id of ['TX-1001','TX-2001']){
      const {p,c}=await context(id,390);
      for(const width of [320,390,430,768,1440]){await p.setViewportSize({width,height:900});assert(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),id+' overflow '+width);}
      await p.setViewportSize({width:390,height:900});
      if(id==='TX-1001'){
        await p.getByRole('button',{name:/Visszajelzésre váró edzés/}).first().click();await p.getByRole('button',{name:'Visszajelzést küldök'}).click();
        const slider=p.locator('[name="mood"]');await slider.evaluate(el=>{const set=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;set.call(el,'2.37');el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));});
        // Use an actual pointer drag so React receives native range input events.
        const box=await slider.boundingBox();await p.mouse.move(box.x+20,box.y+box.height/2);await p.mouse.down();await p.mouse.move(box.x+box.width*.63,box.y+box.height/2,{steps:30});await p.mouse.up();assert(Number(await slider.inputValue())%1!==0);
        await p.screenshot({path:'/tmp/eto-react-review-phone.png'});await p.getByRole('button',{name:'Visszajelzés küldése',exact:true}).click();await p.getByRole('dialog').waitFor({state:'hidden'});
      }else {await p.locator('.priority-card').first().click();await p.locator('.roster li').first().waitFor();await p.getByRole('button',{name:'Bezárás',exact:true}).click();}
      await p.getByRole('link',{name:'Megjelenés & ötletek'}).click();await p.getByRole('radio',{name:'Sötét',exact:true}).check();await p.waitForFunction(()=>document.documentElement.dataset.theme==='dark');await p.getByRole('button',{name:'Palakék',exact:false}).click();await p.waitForFunction(()=>document.documentElement.dataset.palette==='slate');await p.reload();await p.waitForFunction(()=>document.documentElement.dataset.palette==='slate');
      await p.getByLabel('Az ötleted').fill('Saved per-account idea');await p.getByRole('button',{name:'Ötlet mentése'}).click();await p.locator('.idea-list').getByText('Saved per-account idea').waitFor();
      await p.getByRole('link',{name:'Naptár',exact:true}).click();await p.screenshot({path:`/tmp/eto-react-${id}-phone.png`});
      if(id==='TX-2001'){await p.goto(f.base+'/logs');await p.getByRole('heading',{name:'Ez a terület védett'}).waitFor();}
      await p.goto(f.base+'/missing-page');await p.getByRole('heading',{name:'Itt nincs mit keresni'}).waitFor();
      await c.close();console.log('PASS',id,'phone layouts, continuous reviews/roster, persistent themes/ideas, access and missing pages');
    }
    assert.deepEqual(errors,[]);console.log('PASS no uncaught browser errors');
  }finally{if(browser)await browser.close();await f.close();}
})().catch(e=>{console.error(e);process.exit(1)});
