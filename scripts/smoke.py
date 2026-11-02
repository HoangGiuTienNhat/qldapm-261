"""Real HTTP smoke test against the local Docker stack. No third-party packages."""
import json, uuid, urllib.request, urllib.error, http.cookiejar, os
from pathlib import Path

BASE=os.getenv('BASE_URL','http://127.0.0.1:3000')
PASSWORD=os.getenv('DEMO_PASSWORD','RestaurantDemo!2026')
checks=[]
class Client:
    def __init__(self,guest=False):
        self.guest=guest
        self.opener=urllib.request.build_opener(urllib.request.ProxyHandler({}),urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    def call(self,method,path,body=None,key=None,expected=200):
        headers={'Content-Type':'application/json'}
        if self.guest: headers['X-Actor']='guest'
        if method!='GET':
            headers['X-CSRF-TOKEN']=self.call('GET','/auth/csrf')['token']
            headers['Idempotency-Key']=key or str(uuid.uuid4())
        request=urllib.request.Request(BASE+'/api/v1'+path,data=None if method=='GET' else json.dumps(body or {}).encode(),headers=headers,method=method)
        try:
            with self.opener.open(request,timeout=30) as response: status=response.status; data=json.load(response)
        except urllib.error.HTTPError as error: status=error.code; data=json.load(error)
        if status!=expected: raise AssertionError(f'{method} {path}: expected {expected}, got {status}: {data}')
        recorded_path='/qr/<redacted>' if path.startswith('/qr/') else path
        checks.append(f'{method} {recorded_path}: {status}')
        return data
    def login(self,role):return self.call('POST','/auth/login',{'username':role,'password':PASSWORD})

def main():
    admin,waiter,kitchen,cashier,guest=Client(),Client(),Client(),Client(),Client(True)
    for client,role in [(admin,'admin'),(waiter,'waiter'),(kitchen,'kitchen'),(cashier,'cashier')]: client.login(role)
    menu=admin.call('GET','/menu'); assert len(menu['items'])>=20
    food=next(i for i in menu['items'] if i['isAvailable'])
    row={'menuItemId':food['id'],'quantity':2,'observedPriceVnd':food['priceVnd'],'note':'Ít cay, không hành'}
    table=next(t for t in admin.call('GET','/tables')['items'] if not t['visit'] and t['isActive'])
    qr=waiter.call('GET',f"/tables/{table['id']}/qr")
    visit_key=str(uuid.uuid4())
    visit=waiter.call('POST',f"/tables/{table['id']}/visits",{'partySize':2},visit_key)
    replay=waiter.call('POST',f"/tables/{table['id']}/visits",{'partySize':2},visit_key)
    assert visit==replay,'Open visit replay mismatch'
    guest.call('POST','/guest/join',{'qrToken':qr['qrToken']})
    oid=visit['orderId']; order=guest.call('GET',f'/orders/{oid}')
    request_key=str(uuid.uuid4());body={'expectedOrderVersion':order['version'],'items':[row]}
    first=guest.call('POST',f'/orders/{oid}/batches',body,request_key)
    assert first['status']=='APPROVED'
    assert guest.call('POST',f'/orders/{oid}/batches',body,request_key)==first
    guest.call('POST',f'/orders/{oid}/batches',{**body,'items':[{**row,'quantity':3}]},request_key,409)
    order=guest.call('GET',f'/orders/{oid}')
    additional=guest.call('POST',f'/orders/{oid}/batches',{'expectedOrderVersion':order['version'],'items':[row]})
    assert additional['status']=='PENDING'
    assert all(i['batchId']!=additional['batchId'] for i in kitchen.call('GET','/kitchen/items')['items'])
    kitchen.call('POST',f"/batches/{additional['batchId']}/approve",{'expectedVersion':additional['version']})
    order=guest.call('GET',f'/orders/{oid}')
    # Access control and checkout guard.
    guest.call('GET',f'/orders/{uuid.uuid4()}',expected=404)
    waiter.call('POST',f'/orders/{oid}/payments',{'expectedVersion':order['version'],'method':'CASH','amountVnd':food['priceVnd']*4,'tenderedVnd':500000},expected=403)
    guest.call('POST',f'/orders/{oid}/request-payment',{'expectedVersion':order['version']},expected=409)
    items=[i for batch in order['batches'] for i in batch['items']]
    for item in items:
        current=item
        for action in ['accept','prepare']:
            current=kitchen.call('POST',f"/order-items/{item['id']}/{action}",{'expectedVersion':current['version']})
        guest.call('POST',f"/order-items/{item['id']}/cancel",{'expectedVersion':current['version']},expected=403)
        current=kitchen.call('POST',f"/order-items/{item['id']}/ready",{'expectedVersion':current['version']})
        waiter.call('POST',f"/order-items/{item['id']}/serve",{'expectedVersion':current['version']})
    order=guest.call('GET',f'/orders/{oid}')
    checkout=guest.call('POST',f'/orders/{oid}/request-payment',{'expectedVersion':order['version']})
    assert checkout['totalVnd']==food['priceVnd']*4
    payment_key=str(uuid.uuid4());pay={'expectedVersion':checkout['version'],'method':'CASH','amountVnd':checkout['totalVnd'],'tenderedVnd':1000000}
    payment=cashier.call('POST',f'/orders/{oid}/payments',pay,payment_key)
    assert cashier.call('POST',f'/orders/{oid}/payments',pay,payment_key)==payment
    guest.call('GET','/guest/current',expected=401)
    receipt=cashier.call('GET',f'/orders/{oid}/receipt'); assert receipt['order']['status']=='PAID'
    assert next(t for t in waiter.call('GET','/tables')['items'] if t['id']==table['id'])['visit'] is None
    # Manager/admin self approval is authorized by the project owner.
    takeaway=admin.call('POST','/takeaway-orders',{'pickupName':'Nguyễn Minh Anh','items':[{**row,'quantity':1}]})
    oid2=takeaway['orderId'];order=admin.call('GET',f'/orders/{oid2}')
    extra=admin.call('POST',f'/orders/{oid2}/batches',{'expectedOrderVersion':order['version'],'items':[row]})
    admin.call('POST',f"/batches/{extra['batchId']}/approve",{'expectedVersion':extra['version']})
    order=admin.call('GET',f'/orders/{oid2}')
    # Reject and withdraw additional batches.
    extra2=admin.call('POST',f'/orders/{oid2}/batches',{'expectedOrderVersion':order['version'],'items':[row]})
    kitchen.call('POST',f"/batches/{extra2['batchId']}/reject",{'expectedVersion':extra2['version'],'reason':'Khách đã đổi ý, không gọi thêm.'})
    order=admin.call('GET',f'/orders/{oid2}')
    extra3=admin.call('POST',f'/orders/{oid2}/batches',{'expectedOrderVersion':order['version'],'items':[row]})
    admin.call('POST',f"/batches/{extra3['batchId']}/withdraw",{'expectedVersion':extra3['version']})
    # Full cancellation before cooking leaves no orphan visit or bill.
    order=admin.call('GET',f'/orders/{oid2}')
    admin.call('POST',f'/orders/{oid2}/cancel',{'expectedVersion':order['version'],'reason':'Đơn kiểm tra API, khách đổi lịch nhận.'})
    assert admin.call('GET',f'/orders/{oid2}')['status']=='CANCELLED'
    # Out of stock is rejected atomically.
    current_menu=admin.call('GET','/manage/menu-items')
    unavailable=next((i for i in current_menu['items'] if not i['isAvailable']),None)
    temporary=None
    if unavailable is None:
        candidate=next(i for i in current_menu['items'] if i['id']==food['id'])
        unavailable=admin.call('PATCH',f"/menu-items/{candidate['id']}/availability",{'expectedVersion':candidate['version'],'isAvailable':False})
        temporary=unavailable
    try:
        admin.call('POST','/takeaway-orders',{'pickupName':'Khách thử món hết','items':[{'menuItemId':unavailable['id'],'quantity':1,'observedPriceVnd':unavailable['priceVnd']}]},expected=409)
    finally:
        if temporary:admin.call('PATCH',f"/menu-items/{temporary['id']}/availability",{'expectedVersion':temporary['version'],'isAvailable':True})
    # Staff forbidden to manage accounts.
    waiter.call('GET','/staff',expected=403)
    waiter.call('GET','/checkout/orders',expected=403)
    # Bank transfer and ZERO settlement, plus post-preparation cancellation guard.
    for method in ['BANK_TRANSFER','ZERO']:
        created=admin.call('POST','/takeaway-orders',{'pickupName':'Khách thử '+method,'items':[{**row,'quantity':1}]})
        test_oid=created['orderId'];o=admin.call('GET',f'/orders/{test_oid}');item=o['batches'][0]['items'][0]
        current=kitchen.call('POST',f"/order-items/{item['id']}/accept",{'expectedVersion':item['version']})
        current=kitchen.call('POST',f"/order-items/{item['id']}/prepare",{'expectedVersion':current['version']})
        if method=='ZERO':
            admin.call('POST',f"/order-items/{item['id']}/cancel",{'expectedVersion':current['version'],'reason':'Món chế biến không đạt, quản lý miễn phí và hủy.'})
            o=admin.call('GET',f'/orders/{test_oid}')
            admin.call('POST',f'/orders/{test_oid}/cancel',{'expectedVersion':o['version'],'reason':'Thử chặn hủy sau chế biến.'},expected=409)
        else:
            current=kitchen.call('POST',f"/order-items/{item['id']}/ready",{'expectedVersion':current['version']})
            cashier.call('POST',f"/order-items/{item['id']}/serve",{'expectedVersion':current['version']})
        o=admin.call('GET',f'/orders/{test_oid}')
        checkout=admin.call('POST',f'/orders/{test_oid}/request-payment',{'expectedVersion':o['version']})
        expected_amount=food['priceVnd'] if method=='BANK_TRANSFER' else 0
        assert checkout['totalVnd']==expected_amount
        cashier.call('POST',f'/orders/{test_oid}/payments',{'expectedVersion':checkout['version'],'method':method,'amountVnd':expected_amount,'tenderedVnd':None,'transferReference':'VCB-DEMO-20261102-153022' if method=='BANK_TRANSFER' else None})
        assert cashier.call('GET',f'/orders/{test_oid}/receipt')['method']==method
    result={'result':'PASS','http_checks':len(checks),'checks':checks,'dineInOrderId':oid,'cancelledTakeawayOrderId':oid2}
    target=Path(__file__).resolve().parents[1]/'docs'/'verification'/'smoke-result.json';target.parent.mkdir(parents=True,exist_ok=True);target.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'PASS: {len(checks)} HTTP checks. Dine-in payment, approval, self approval, retry, RBAC, cancellation, unavailable item.')
if __name__=='__main__': main()
