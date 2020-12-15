<p align="center">
<a href=" https://www.alibabacloud.com"><img src="https://aliyunsdk-pages.alicdn.com/icons/Aliyun.svg"></a>
</p>

<h1 align="center">企业工作台 .net SDK </h1>


## 基本原理

在官网 SDK 的基础上，对 Client进行重写，满足企业工作台的调用逻辑，同时完全兼容官网 SDK，这样就形成了 企业工作台定制 Client + 官网 SDK 提供 APIMETA 的模式。


## 环境要求

- 找阿里云企业工作台团队，提供 OpenAPI 访问凭证(consoleKey、consoleSecret)


## 快速使用 

企业工作台的业务模式分为 工作台托管、聚石塔自管 两种模式，因此API调用也有针对性区分。


### 工作台托管 SDK 调用示例

```net
using Aliyun.Acs.Core.Profile;
using Aliyun.Acs.Ecs.Model.V20140526;
using isv_net_sdk;

namespace test_oneConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            // 企业工作台定制SDK
            DefaultProfile profile = DefaultProfile.GetProfile(
                ${regionId},
                ${consoleKey},
                ${consoleSecret}
            );

            ConsoleAcsClient client = new ConsoleAcsClient(profile);
            client.AddQueryParam("AliUid", "xxx");
            client.Endpoint = "console-bench.aliyuncs.com";

            // 创建API请求并设置参数
            DescribeInstancesRequest request = new DescribeInstancesRequest();
            var resp = client.GetAcsResponse(request);
        }
    }
}

```

说明：

- endpoint: 测试环境下需要 host 绑定 114.55.202.134 console-bench.aliyuncs.com


### 聚石塔托管 SDK 调用示例

```python
using Aliyun.Acs.Core.Profile;
using Aliyun.Acs.Ecs.Model.V20140526;
using isv_net_sdk;

namespace test_oneConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            // 企业工作台定制SDK
            DefaultProfile profile = DefaultProfile.GetProfile(
                ${regionId},
                ${consoleKey},
                ${consoleSecret}
            );

            
            ConsoleAcsClient client = new ConsoleAcsClient(profile);
            client.AddQueryParam("IdToken", "xxx");
            client.Endpoint = "114.55.202.134";

            // 创建API请求并设置参数
            DescribeInstancesRequest request = new DescribeInstancesRequest();
            var resp = client.GetAcsResponse(request);
        }
    }
}
```

说明：

- endpoint: 测试环境下需要 host 绑定 114.55.202.134 console-bench.aliyuncs.com


## 许可证

[Apache-2.0](http://www.apache.org/licenses/LICENSE-2.0)

Copyright (c) 2009-present, Alibaba Cloud All rights reserved.