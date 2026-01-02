import publish

context = publish.get_context()
publish.publish(context, 'linux-x64')
print('Done (run_publish_linux)')