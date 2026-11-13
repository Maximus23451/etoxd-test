const test = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const {webcrypto} = require('node:crypto');
function fixture() {
  const values = new Map();
  const storage = {getItem:key=>values.get(key) ?? null,setItem:(key,value)=>values.set(key,value),removeItem:key=>values.delete(key)};
  const window = {localStorage:storage,sessionStorage:storage};
  vm.runInNewContext(fs.readFileSync(require('node:path').join(__dirname,'../data.js'),'utf8'), {window,crypto:webcrypto});
  const store = window.EtoStore;
  const state = store.load();
  return {store,state,manager:state.users.find(u=>u.role==='manager').id};
}
test('manager creates an admin and records the change',()=>{
  const {store,state,manager}=fixture();
  assert.equal(store.createUser(state,manager,{name:'Test Admin',talentx:'TX-9999',role:'admin'}),null);
  assert.equal(store.authenticate(state,'TX-9999','eto1904').user.role,'admin');
  assert.equal(state.audit[0].actorId,manager);
});
test('admin cannot create admins/managers, alter privileged users, or change own role',()=>{
  const {store,state,manager}=fixture();
  for(const role of ['admin','manager']) assert.ok(store.createUser(state,'a1',{name:'Escalation',talentx:'TX-9999',role}));
  assert.ok(store.updateUser(state,'a1',manager,{active:false}));
  assert.ok(store.updateUser(state,'a1','a1',{role:'manager'}));
  assert.ok(store.updateUser(state,'a1','p1',{role:'admin'}));
  assert.equal(state.users.find(u=>u.id==='p1').role,'player');
});
test('self-disable and inactive actors are rejected',()=>{
  const {store,state,manager}=fixture();
  assert.ok(store.updateUser(state,manager,manager,{active:false}));
  state.users.find(u=>u.id===manager).active=false;
  assert.ok(store.createRole(state,manager,'Inactive','admin'));
});
test('custom roles inherit scope and privileged custom roles remain manager-only',()=>{
  const {store,state,manager}=fixture();
  assert.ok(store.createRole(state,'a1','Test','admin'));
  assert.equal(store.createRole(state,manager,'Lead admin','admin'),null);
  const role=state.customRoles[0].id;
  assert.equal(store.baseRole(state,role),'admin');
  assert.ok(store.createUser(state,'a1',{name:'Escalation',talentx:'TX-9999',role}));
  assert.equal(store.createUser(state,manager,{name:'Lead',talentx:'TX-9999',role}),null);
  assert.ok(store.updateUser(state,'a1',state.users.at(-1).id,{active:false}));
});
test('roles and accounts persist when demo schedules refresh',()=>{
  const {store,state,manager}=fixture();
  store.createRole(state,manager,'Assistant','coach');
  store.createUser(state,manager,{name:'New coach',talentx:'TX-9999',role:state.customRoles[0].id,team:'U17'});
  state.seededAt='2020-01-01T00:00:00Z'; store.save(state);
  const loaded=store.load();
  assert.ok(loaded.users.some(u=>u.talentx==='TX-9999'));
  assert.equal(loaded.customRoles.length,1); assert.equal(loaded.audit.length,2);
});
test('old browser data receives one manager without losing users',()=>{
  const {store,state}=fixture();
  state.users=state.users.filter(u=>u.role!=='manager'); delete state.customRoles; delete state.audit;
  store.save(state); const loaded=store.load();
  assert.equal(loaded.users.filter(u=>u.role==='manager').length,1);
  assert.ok(loaded.users.some(u=>u.id==='p1')); assert.equal(store.load().users.length,loaded.users.length);
});
test('invalid role, duplicate ID and missing team are rejected without audit entries',()=>{
  const {store,state,manager}=fixture();
  assert.ok(store.createUser(state,manager,{name:'Duplicate',talentx:'tx-1001',role:'player',team:'U17'}));
  assert.ok(store.createUser(state,manager,{name:'No team',talentx:'TX-9999',role:'coach',team:''}));
  assert.ok(store.createUser(state,manager,{name:'Unknown',talentx:'TX-9999',role:'unknown'}));
  assert.equal(state.audit.length,0);
});
