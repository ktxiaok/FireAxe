import publish

context = publish.get_context()
publish.publish(context, 'win-x64')
publish.publish(context, None)
print('Done (run_publish)')