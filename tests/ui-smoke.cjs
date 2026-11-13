// Run with a local server on port 8000 and Playwright + Chromium installed.
const {chromium} = require('playwright');
const assert = require('node:assert/strict');
(async()=>{
  const browser=await chromium.launch({headless:true,executablePath:process.env.CHROMIUM_PATH || '/usr/bin/chromium'});
  const errors=[];
  async function pageFor(id,width=390) {
    const context=await browser.newContext({viewport:{width,height:844},timezoneId:'Europe/Budapest',isMobile:width<760,hasTouch:width<760});
    const page=await context.newPage(); page.on('pageerror',e=>errors.push(e.message));
    await page.clock.install({time:new Date('2026-11-13T13:00:00Z')});
    await page.goto('http://127.0.0.1:8000');await page.click(`[data-demo-id="${id}"]`);await page.waitForURL('**/app.html');
    return {page,context};
  }
  const {page:m,context:mc}=await pageFor('TX-0001',1440);
  assert.match(await m.locator('#user-role').innerText(),/Rendszergazda/i);
  await m.click('[data-tab="roles"]');
  await m.locator('[data-form="add-role"] [name="name"]').fill('Club director');
  await m.locator('[data-form="add-role"] [name="base"]').selectOption('admin');
  await m.locator('[data-form="add-role"] button').click();
  assert.match(await m.locator('.role-list').first().innerText(),/Club director/);
  await m.click('[data-tab="users"]');
  const create=m.locator('[data-form="add-user"]');
  await create.locator('[name="name"]').fill('Test Admin');await create.locator('[name="talentx"]').fill('TX-9999');
  await create.locator('[name="role"]').selectOption('admin');await create.locator('button').click();
  let row=m.locator('tr').filter({hasText:'Test Admin'});
  await row.locator('[name="role"]').selectOption({label:'Club director'});await row.locator('button[type="submit"]').click();
  await m.reload();await m.click('[data-tab="users"]');
  row=m.locator('tr').filter({hasText:'Test Admin'});assert.match(await row.innerText(),/Club director/);
  assert(await m.locator('tr').filter({hasText:'ETO Rendszergazda'}).locator('[role="switch"]').isDisabled());
  await m.click('[data-tab="roles"]');assert.match(await m.locator('.role-list').last().innerText(),/Fiók létrehozása/);
  await m.click('#logout');await m.waitForURL('**/index.html');await m.fill('#username','TX-9999');await m.fill('#password','eto1904');await m.click('.submit-button');await m.waitForURL('**/app.html');
  assert.equal(await m.locator('[data-tab="roles"]').count(),0);await m.click('[data-tab="users"]');
  assert.equal(await m.locator('[data-form="add-user"] [name="role"] option[value="admin"]').count(),0);
  assert(await m.locator('tr').filter({hasText:'Kovács Eszter'}).locator('[role="switch"]').isDisabled());
  console.log('PASS manager creates roles/admins, assigns roles, audit and persistence; custom admin inherits restrictions');await mc.close();
  for(const id of ['TX-1001','TX-2001']) {
    const {page:p,context}=await pageFor(id);
    for(const width of [320,390,430,768,1440]) {
      await p.setViewportSize({width,height:844});
      assert(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`${id}: horizontal overflow at ${width}`);
    }
    await p.setViewportSize({width:390,height:844});
    if(id==='TX-1001') {
      await p.locator('.priority-section [data-act="checkin"]').first().click();
      await p.locator('.priority-section [data-act="checkout"]').first().click();
      await p.locator('[data-act="feedback"]').first().click();
      const slider=p.locator('[name="mood"]');
      await slider.evaluate(el=>{el.value='1.25';el.dispatchEvent(new Event('input',{bubbles:true}));});
      const first=await p.locator('.face-lg .mouth').getAttribute('d');
      await slider.evaluate(el=>{el.value='1.75';el.dispatchEvent(new Event('input',{bubbles:true}));});
      assert.notEqual(first,await p.locator('.face-lg .mouth').getAttribute('d'));
      assert.equal(await slider.getAttribute('step'),'0.01');
      const box=await slider.boundingBox();const values=[];
      await slider.evaluate(el=>{window.sliderValues=[];el.addEventListener('input',()=>window.sliderValues.push(+el.value));});
      await p.mouse.move(box.x+18,box.y+box.height/2);await p.mouse.down();await p.mouse.move(box.x+box.width-18,box.y+box.height/2,{steps:40});await p.mouse.up();
      values.push(...await p.evaluate(()=>window.sliderValues));assert(values.length>20);assert(values.some(v=>v%1!==0));
      await slider.evaluate(el=>{el.value='2.37';el.dispatchEvent(new Event('input',{bubbles:true}));});
      await p.locator('[name="load"]').evaluate(el=>{el.value='1.61';el.dispatchEvent(new Event('input',{bubbles:true}));});
      await p.locator('.fb-form button[type="submit"]').click();
      assert.equal(await p.evaluate(()=>window.EtoStore.load().feedback.at(-1).answers.mood),2.37);
      await p.reload();assert.equal(await p.evaluate(()=>window.EtoStore.load().feedback.at(-1).answers.load),1.61);
      await p.screenshot({path:'/tmp/eto-player-phone.png',fullPage:true});
    } else {
      await p.selectOption('[data-filter="period"]','live');await p.locator('.staff-card').first().evaluate(el=>el.open=true);
      assert(await p.locator('.staff-card .roster li').count()>0);
      await p.screenshot({path:'/tmp/eto-coach-phone.png',fullPage:true});
    }
    console.log('PASS',id,'mobile widths, key actions and no browser errors');await context.close();
  }
  assert.deepEqual(errors,[]);await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
