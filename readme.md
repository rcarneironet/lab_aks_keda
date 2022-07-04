# Introdução

Um cluster Kubernetes necessita de recursos computacionais para rodar as aplicações e esses recursos normalmente podem aumentar ou diminuir baseado em cargas de trabalho ou eventuais picos de utilização das aplicações. Esse termo normalmente é denominado como "escala".
A escala nesse contexto pode ser realizada no próprio cluster ou no nível de aplicação.

Em cenários de aumento de carga, por exemplo tráfego maior nas aplicações, é possível escalarmos as aplicações de forma "horizontal", ou seja, criando novas instâncias para atender a demanda sem acréscimo de memória ou CPU.

No Kubernetes podemos utilizar o recurso do [Horizontal Pod Autoscaling](https://kubernetes.io/docs/tasks/run-application/horizontal-pod-autoscale/) para atualizar automaticamente a carga de trabalho de algum recurso, ajustando a escala para atender às métricas especificadas.

Com o [KEDA](https://keda.sh/) podemos estender as funcionalidades do Horizontal Pod Autoscaling e trabalhar com [Arquiteturas baseadas em eventos](https://docs.microsoft.com/azure/architecture/guide/architecture-styles/event-driven).

## Pré-requisitos

Para uma total compreensão desse artigo ou realização de uma demo em seu ambiente é necessário entender os conceitos e provisionar uma fila no [Azure Service Bus](https://docs.microsoft.com/azure/service-bus-messaging/service-bus-messaging-overview), um cluster no [AKS](https://azure.microsoft.com/services/kubernetes-service/), um [Azure Container Registry](https://azure.microsoft.com/services/container-registry/) com uma imagem de aplicação já enviada e saber como utilizar o terminal [Azure Cloud Shell](https://docs.microsoft.com/azure/cloud-shell/overview).

## Download do projeto exemplo

O projeto exemplo desse artigo pode ser encontrado nesse [repositório](https://github.com/rcarneironet/lab_aks_keda) no Github. O código aqui disponibilizado é meramente para efeitos de experimentação e não representa uma recomendação de código pronto para produção pela Microsoft.

## Configuração do projeto exemplo

Caso deseje criar um laboratório em seu ambiente, será necessário provisionar a infraestrutura mencionada na seção de "pre-requisitos" acima e instalar o [.NET 6](https://dotnet.microsoft.com/download/dotnet/6.0) na sua máquina local.

Com o Azure Service bus criado, crie [uma fila](https://docs.microsoft.com/azure/service-bus-messaging/service-bus-quickstart-portal) com o nome de "kedafila" e obtenha a string de conexão no Azure.

Também será necessário criar um container de alguma aplicação e enviar uma imagem para o seu ACR. [Maiores detalhes de como fazer pode ser encontrado nesse artigo](https://docs.microsoft.com/azure/container-registry/container-registry-get-started-docker-cli?tabs=azure-cli).

Lembre-se também de habilitar a comunicação entre o cluster AKS e o Azure Container Registry, caso não tenha feito utilizando o comando abaixo via linha de comando no Azure:

```powershell
az aks update -n <seu_cluster> -g <seu_resource_group> --attach-acr <seu_acr>
```

Após o download do projeto exemplo, será necessário configurar os seguintes arquivos:

- k8s/deployment.yml: nesse arquivo será necessário incluir o caminho da imagem do [Azure Container Registry](https://azure.microsoft.com/services/container-registry/) e a string de conexão do seu serviço [Azure Service Bus](https://docs.microsoft.com/azure/service-bus-messaging/service-bus-messaging-overview)
- k8s/scaling.yml: nesse arquivo será necessário incluir a string de conexão do seu serviço [Azure Service Bus](https://docs.microsoft.com/azure/service-bus-messaging/service-bus-messaging-overview) em formato [base64](https://wikipedia.org/wiki/Base64) e o nome do namespace criado no Service Bus.
- k8s/secret.yml: nesse arquivo será necessário incluir a string de conexão do seu serviço [Azure Service Bus](https://docs.microsoft.com/azure/service-bus-messaging/service-bus-messaging-overview) em formato [base64](https://wikipedia.org/wiki/Base64).

Na pasta "src" do projeto temos dois sub-projetos, sendo eles:

- Keda.Functions.Events: Esse projeto é uma [Azure Function](https://azure.microsoft.com/services/functions/) que tem a responsabilidade de se conectar na fila do Azure Services Bus e enviar mensagens. Altere o arquivo "ServiceBusSendMessage.cs" para incluir a sua string de conexão.

- Keda.Worker.ConsumeEvents: Esse projeto é um Console que é executado em background e se conecta na fila do Azure Service bus para consumir as mensagens de forma assíncrona. É preciso alterar o arquivo "Worker.cs" nesse projeto e incluir a sua string de conexão ao Azure Service Bus.

É recomendado que você defina ambos os projetos para inicializar juntos na propriedade da solução no [Visual Studio](https://visualstudio.microsoft.com/vs/) para que [ambos iniciem ao mesmo tempo](https://docs.microsoft.com/visualstudio/ide/how-to-set-multiple-startup-projects?view=vs-2022).

## KEDA (Kubernetes Event-driven Autoscaling)

O [KEDA](https://keda.sh/) é um autoscaler do Kubernetes, com ele podemos realizar a escala de qualquer container no Kubernetes baseado em uma série de possíveis eventos que possam ocorrer e que precisarão de processamento. O KEDA é um componente que pode ser adicionado a qualquer cluster Kubernetes e  estende as funcionalidades do [Horizontal Pod autoscaler](https://kubernetes.io/docs/tasks/run-application/horizontal-pod-autoscale/).

Alguns dos scalers disponíveis podem ser encontrados na [documentação oficial](https://keda.sh/docs/2.7/scalers/).

## Arquitetura em alto nível da proposta

Em um alto nível o KEDA provê um componente que ativa e desativa um deployment para escalar os recursos quando não existem mais eventos. O KEDA também provê um serviço de métricas que expõe os eventos, tal como uma fila, um tópico, métricas de CPU ou memória, por exemplo. Abaixo, veremos a proposta de arquitetura de solução para esse cenário:

![arquitetura-keda-aks](highlevel-architecture.png)

No exemplo da arquitetura proposta acima temos uma [Azure Function](https://docs.microsoft.com/azure/azure-functions/) que envia mensagens para um [Azure Service Bus](https://docs.microsoft.com/azure/service-bus-messaging/service-bus-messaging-overview) e um serviço (worker) em [.NET 6](https://dotnet.microsoft.com/download/dotnet/6.0) que executa em background e consome as mensagens de forma assíncrona.

Para que o autoscale funcione, é necessário instalar o KEDA no cluster Kubernetes e criar um Deployment e um ScaledObject. Veremos mais a frente os detalhes da implementação desse cenário. O KEDA praticamente executa duas ações com o Kubernetes:

1. Agent: o KEDA ativa ou desativa os deployments do Kubernetes para escalar
2. Metrics Adapter: Responsável por expor os dados dos eventos para o Horizontal Pod Autoscaler visando a escala.

## Instalação do KEDA em um cluster Kubernetes

No AKS, ao criar um cluster, temos disponível o [HELM (gerenciador de pacote do Kubernetes)](https://helm.sh/). Abra um terminal [Azure Cloud Shell](https://docs.microsoft.com/azure/cloud-shell/overview) e faça login no seu cluster Kubernetes, em seguida, execute os seguintes comandos:

```powershell
helm repo add kedacore https://kedacore.github.io/charts
helm repo update
kubectl create namespace keda
helm install keda kedacore/keda --namespace keda
```

Os comandos deverão fazer download do pacote do KEDA em seu cluster e criar os pods dentro do namespace "keda". Para verificar execute o comando a seguir:

```powershell
kubectl get pod -n keda
```

Executando o comando acima você verá o Pod que foi criado dentro do namespace "keda".

## Execução dos deployments no AKS

Precisaremos agora fazer o deployment dos objetos no AKS, dessa forma, faça login no Azure Cloud Shell, faça download do [repositório Git](https://github.com/rcarneironet/lab_aks_keda) e dentro da pasta "k8s" onde se encontra o arquivo "deployment.yml", assegure-se de ter alterado os parâmetros mencionados na seção "Configuração do projeto exemplo" acima e execute o comando a seguir:

```powershell
kubectl apply -f deployment.yml -n kedaexemplo
```

Verifique se o pod foi criado:

```powershell
kubectl get pod -n kedaexemplo
```

No mesmo terminal previamente aberto faremos a configuração do arquivo "scaling.yml" para que o KEDA monitore a fila do Service Bus baseado nas configurações citadas no arquivo. Aplicaremos essas configurações com o comando abaixo:

```powershell
kubectl apply -f scaling.yml -n kedaexemplo
```

Verifique se o ScaleObject está com o status READY igual a TRUE. Isso significa que está tudo certo para realizar o autoscaling.

```powershell
kubectl get scaledobjects -n kedaexemplo
```

## Execução da demo

Se todas as etapas acima citadas tiveram êxito, execute os projetos "Keda.Function.SendEvents" e "Keda.Worker.ConsumeEvents" localmente com o Visual Studio. O projeto que contém a Azure Function fará um loop e enviará mensagens para a fila do Service Bus e o projeto Worker fará o consumo das mensagens. Recomenda-se enviar um número considerável de mensagens para que possamos observar o comportamento de autoscaling dos Pods, que serão criados para atender a demanda de mensagens enviadas para o Service Bus. Durante a execução dos projetos, observe o provisionamento de novos Pods executando o comando:

```powershell
kubectl get pod -n kedaexemplo
```

O resultado da execução será semelhante ao da figura abaixo onde a escala horizontal acontece mediante os parâmetros informados na configuração do KEDA no cluster do Kubernetes. A medida que as mensagens são enviadas para a fila, o autoscale (up e down) acontece para atender a carga de trabalha que nesse cenário é baseada em eventos.

A medida que as mensagens são enviadas e a carga de trabalho aumenta, o KEDA irá provisionar novos PODs para atender a demanda realizando um autoscale (up). A medida que as mensagens na fila são consumidas com sucesso pelos consumidores e a carga de trabalho nos PODs diminui, o KEDA fará um autoscale novamente para remover os PODs.

![arquitetura-keda-aks](resultado.png)

## Considerações Finais

A estratégia citada nesse artigo visa realizar autoscaling em clusters Kubernetes em cenários de event-driven, provisionando a infraestrutura necessária para atender as demandas em picos de utilização de recursos e maximizando custos de processamento na infraestrutura.
