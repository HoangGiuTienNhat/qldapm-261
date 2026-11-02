"""Real HTTP checks for immutable recipient snapshots and admin payment history."""
import json,uuid
from datetime import datetime,timedelta,timezone
from pathlib import Path
from smoke import Client,PASSWORD,checks

def main():
    admin,cashier,waiter,kitchen,manager,anonymous=Client(),Client(),Client(),Client(),Client(),Client()
    admin.login('admin');waiter.login('waiter');manager.login('manager');kitchen.login('kitchen')
    anonymous.call('GET','/admin/payments',expected=401)
    for client in [waiter,manager,kitchen]:client.call('GET','/admin/payments',expected=403)
    previous=admin.call('GET','/admin/payments?page=1&pageSize=3')
    assert previous['pageSize']==3
    previous_ids=[r['paymentId'] for r in previous['items']]
    uid=uuid.uuid4().hex[:8]
    username='cashier_hist_'+uid
    name='Trần Thu Ngân - kiểm thử '+uid
    staff=admin.call('POST','/staff',{'username':username,'displayName':name,'initialPassword':PASSWORD,'roles':['CASHIER']})
    cashier.call('POST','/auth/login',{'username':username,'password':PASSWORD})
    cashier.call('POST','/auth/change-password',{'currentPassword':PASSWORD,'newPassword':PASSWORD+'-History'})
    cashier.call('GET','/admin/payments',expected=403)
    menu=admin.call('GET','/manage/menu-items');category=next(c for c in menu['categories'] if c['isActive'])
    dish_name='Bún thịt nướng - kiểm thử lịch sử '+uid
    dish=admin.call('POST','/menu-items',{'categoryId':category['id'],'code':'HIST-'+uid.upper(),'name':dish_name,'description':'Bún, thịt nướng, rau sống; dữ liệu kiểm thử riêng.','imageUrl':'/menu/placeholder.svg','priceVnd':99000,'isActive':True})
    row={'menuItemId':dish['id'],'quantity':1,'observedPriceVnd':99000,'note':'Ít dầu, để riêng nước chấm'}
    order=admin.call('POST','/takeaway-orders',{'pickupName':'Nguyễn Minh Anh - kiểm thử lịch sử','items':[row]})
    oid=order['orderId']
    def extra(quantity=1):
        current=admin.call('GET','/orders/'+oid)
        return admin.call('POST','/orders/'+oid+'/batches',{'expectedOrderVersion':current['version'],'items':[{**row,'quantity':quantity}]})
    approved=extra(2);admin.call('POST',f"/batches/{approved['batchId']}/approve",{'expectedVersion':approved['version']})
    rejected=extra();kitchen.call('POST',f"/batches/{rejected['batchId']}/reject",{'expectedVersion':rejected['version'],'reason':'Khách không cần gọi thêm món này.'})
    withdrawn=extra();admin.call('POST',f"/batches/{withdrawn['batchId']}/withdraw",{'expectedVersion':withdrawn['version']})
    current=admin.call('GET','/orders/'+oid)
    first=current['batches'][0]['items'][0]
    admin.call('POST',f"/order-items/{first['id']}/cancel",{'expectedVersion':first['version'],'reason':'Khách đổi sang đợt gọi sau.'})
    item=current['batches'][1]['items'][0]
    for action in ['accept','prepare','ready']:
        item=kitchen.call('POST',f"/order-items/{item['id']}/{action}",{'expectedVersion':item['version']})
    cashier.call('POST',f"/order-items/{item['id']}/serve",{'expectedVersion':item['version']})
    current=admin.call('GET','/orders/'+oid)
    checkout=admin.call('POST',f'/orders/{oid}/request-payment',{'expectedVersion':current['version']})
    key=str(uuid.uuid4());payload={'expectedVersion':checkout['version'],'method':'CASH','amountVnd':198000,'tenderedVnd':200000}
    paid=cashier.call('POST',f'/orders/{oid}/payments',payload,key)
    assert cashier.call('POST',f'/orders/{oid}/payments',payload,key)==paid
    receiver={'staffId':staff['id'],'username':username,'displayName':name,'snapshotAvailable':True}
    assert paid['receivedBy']==receiver
    paid_at=datetime.fromisoformat(paid['paidAt'].replace('Z','+00:00'))
    assert abs((datetime.now(timezone.utc)-paid_at).total_seconds())<120
    pid=paid['id']
    detail=admin.call('GET','/admin/payments/'+pid)
    assert detail['receivedBy']==receiver and detail['paidAt']==paid['paidAt']
    assert detail['order']['id']==oid and detail['order']['status']=='PAID'
    assert len(detail['order']['batches'])==4
    assert [b['status'] for b in detail['order']['batches']]==['APPROVED','APPROVED','REJECTED','WITHDRAWN']
    assert detail['order']['batches'][0]['items'][0]['status']=='CANCELLED'
    assert detail['order']['batches'][1]['items'][0]['status']=='SERVED'
    assert detail['amountVnd']==198000 and detail['changeVnd']==2000
    today=datetime.now(timezone(timedelta(hours=7))).date().isoformat()
    listed=admin.call('GET',f'/admin/payments?receivedByStaffId={staff["id"]}&fromDate={today}&toDate={today}&method=CASH&orderType=TAKEAWAY&page=1&pageSize=1')
    assert listed['totalCount']==1 and listed['items'][0]['paymentId']==pid
    assert listed['items'][0]['receivedBy']==receiver
    assert admin.call('GET','/admin/payments?search='+detail['receiptNo'])['totalCount']==1
    assert admin.call('GET','/admin/payments?search='+username)['totalCount']==1
    assert admin.call('GET','/admin/payments?search=%25')['totalCount']==0,'Percent search must be literal, not wildcard'
    for query in ['page=0','pageSize=101','method=CARD','fromDate=2026-11-31','fromDate=2026-11-04&toDate=2026-11-03','receivedByStaffId=not-a-uuid']:
        admin.call('GET','/admin/payments?'+query,expected=422)
    first_page=admin.call('GET','/admin/payments?page=1&pageSize=1')
    assert first_page['items'][0]['paymentId']==pid
    if first_page['totalCount']>1:
        second_page=admin.call('GET','/admin/payments?page=2&pageSize=1')
        assert second_page['items'][0]['paymentId']!=pid
    for client in [waiter,kitchen,manager,cashier]:client.call('GET','/admin/payments/'+pid,expected=403)
    admin.call('GET','/admin/payments/'+str(uuid.uuid4()),expected=404)
    profile=next(u for u in admin.call('GET','/staff')['items'] if u['id']==staff['id'])
    admin.call('PATCH','/staff/'+staff['id'],{'expectedVersion':profile['version'],'displayName':'Hồ sơ đã đổi sau thanh toán '+uid,'active':False})
    admin.call('PATCH','/menu-items/'+dish['id'],{'expectedVersion':dish['version'],'name':'Tên món mới sau thanh toán '+uid,'priceVnd':110000,'isActive':False})
    after=admin.call('GET','/admin/payments/'+pid)
    assert after['receivedBy']==receiver and after['paidAt']==paid['paidAt']
    for batch in after['order']['batches']:
        for old_item in batch['items']:
            assert old_item['menuNameSnapshot']==dish_name and old_item['unitPriceVnd']==99000 and old_item['note']==row['note']
    assert admin.call('GET',f'/orders/{oid}/receipt')['receivedBy']==receiver
    for old_id in previous_ids:admin.call('GET','/admin/payments/'+old_id)
    admin.call('POST',f'/orders/{oid}/reopen',{'expectedVersion':after['order']['version'],'reason':'Không cho mở lại đơn đã thanh toán.'},expected=409)
    result={'result':'PASS','http_checks':len(checks),'checks':checks,'paymentId':pid,'orderId':oid,'scenarios':['admin-only list/detail','complete paid order retained','receiver ID/username/name captured','server payment timestamp','same-key payment replay','restaurant date and receiver/method/type/search filters','literal wildcard search','pagination','validation422','receiver rename/deactivation leaves snapshot unchanged','menu changes leave paid item snapshots unchanged','existing development payments preserved','paid order cannot reopen']}
    Path('docs/verification/payment-history-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'PASS: {len(checks)} HTTP checks for payment history and recipient snapshots.')

if __name__=='__main__':main()
