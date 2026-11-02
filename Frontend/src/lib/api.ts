export class ApiError extends Error {constructor(public code: string, message: string, public status: number){super(message);}}
export const key = () => crypto.randomUUID();
export async function api<T = any>(path: string, options: { method?: string; body?: unknown; guest?: boolean; key?: string } = {}): Promise<T> {
  const method=options.method??"GET";
  const headers: Record<string,string> = { "Content-Type":"application/json", ...(options.guest?{"X-Actor":"guest"}:{}) };
  if(method!=="GET"){
    const csrf=await fetch("/api/v1/auth/csrf",{headers:options.guest?{"X-Actor":"guest"}:{},cache:"no-store"});
    if(!csrf.ok)throw new Error("Không lấy được CSRF token. Kiểm tra kết nối backend.");
    headers["X-CSRF-TOKEN"]=(await csrf.json()).token;
    headers["Idempotency-Key"]=options.key??key();
  }
  const response=await fetch("/api/v1"+path,{method,headers,body:method==="GET"?undefined:JSON.stringify(options.body??{}),cache:"no-store"});
  const data=await response.json().catch(()=>({}));
  if(!response.ok)throw new ApiError(data.code??"REQUEST_FAILED",data.detail??`Yêu cầu thất bại (${response.status})`,response.status);
  return data;
}
export const money=(n:number|null|undefined)=>new Intl.NumberFormat("vi-VN",{style:"currency",currency:"VND",maximumFractionDigits:0}).format(n??0);
export const labels:Record<string,string>={SERVING:"Đang phục vụ",AWAITING_PAYMENT:"Chờ thanh toán",PAID:"Đã thanh toán",CANCELLED:"Đã hủy",PENDING:"Chờ phê duyệt",APPROVED:"Đã duyệt",REJECTED:"Từ chối",WITHDRAWN:"Đã rút",PENDING_APPROVAL:"Chờ phê duyệt",PLACED:"Đã đặt",ACCEPTED:"Bếp đã nhận",PREPARING:"Đang chuẩn bị",READY:"Sẵn sàng",SERVED:"Đã phục vụ"};
