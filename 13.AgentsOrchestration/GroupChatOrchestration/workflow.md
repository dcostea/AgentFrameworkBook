# Workflow Diagram

```mermaid
flowchart TD
  GroupChatHost["GroupChatHost (Start)"];
  MotorsAgent_46e89e5600224e3c9ce5c18bfe678c43["MotorsAgent_46e89e5600224e3c9ce5c18bfe678c43"];
  NavigatorAgent_eb7ee09954344003974895e3d2ea224b["NavigatorAgent_eb7ee09954344003974895e3d2ea224b"];
  GroupChatHost --> MotorsAgent_46e89e5600224e3c9ce5c18bfe678c43;
  GroupChatHost --> NavigatorAgent_eb7ee09954344003974895e3d2ea224b;
  MotorsAgent_46e89e5600224e3c9ce5c18bfe678c43 --> GroupChatHost;
  NavigatorAgent_eb7ee09954344003974895e3d2ea224b --> GroupChatHost;
```
