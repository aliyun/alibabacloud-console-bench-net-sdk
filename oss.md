
## 关于OSS的OpenAPI调用

因为 oss 是数据类型的 OpenAPI，因此我们对其进行了特殊处理，调用方式跟官网SDK存在一些差别

## 获取所有bucket

```
#region commonRequest调用
// 企业工作台定制SDK
DefaultProfile profile = DefaultProfile.GetProfile(${RegionId}, ${consoleKey}, ${consoleSecret});

ConsoleAcsClient client = new ConsoleAcsClient(profile);

client.AddQueryParam("IdToken", "xxx");
client.Endpoint = "114.55.202.134";

Aliyun.Acs.Core.CommonRequest request = new Aliyun.Acs.Core.CommonRequest();
request.Method = Aliyun.Acs.Core.Http.MethodType.GET;
request.Product = "oss";
request.Action = "GetService";
var res = client.GetCommonResponse(request);

```            